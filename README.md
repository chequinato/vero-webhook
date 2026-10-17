# Vero — API de Webhooks com Detecção de Anomalias em Tempo Real

API que recebe transações financeiras via webhook e decide, em tempo real, se
a transação é confiável, suspeita ou deve ser bloqueada imediatamente.

> 📚 **Estudando o projeto?** O guia completo está em [`docs/`](docs/README.md),
> dividido por assunto: C#, Clean Architecture, SOLID, design patterns, EF Core,
> segurança, Machine Learning, React em tempo real, WebGL, AWS, testes e as
> armadilhas encontradas de verdade. Cada arquivo começa com um resumo "pra gravar".

## Modelo híbrido de detecção

- **Síncrono**: Casos óbvios de fraude são bloqueados na hora (resposta 403).
- **Assíncrono**: Casos duvidosos são aceitos provisoriamente (resposta 202) e
  analisados em background por um worker. Se uma anomalia for confirmada, o
  status é atualizado e um alerta é disparado.

## Stack

| Camada | Tecnologia |
|--------|------------|
| Linguagem | C# (.NET 10) |
| Framework | ASP.NET Core |
| Banco de dados | PostgreSQL |
| ORM | Entity Framework Core |
| Fila | AWS SQS (ou simulação em memória) |
| Alertas | AWS SNS (ou console log local) |
| Worker | AWS Lambda (ou BackgroundService local) |
| Testes | xUnit + Moq |
| Infra | Terraform |

## Estrutura do projeto

```
Vero/
├── src/
│   ├── Vero.Domain/              # 🧠 Coração do negócio — não depende de NINGUÉM
│   │   ├── Entities/             #    Transacao, Conta, ...
│   │   ├── Enums/                #    StatusTransacao
│   │   ├── Interfaces/           #    Contratos que o negócio precisa (repositórios, fila, alerta, notificador)
│   │   ├── Policies/             #    PoliticaDeRisco — regra do corte de 70% do modelo
│   │   └── Rules/                #    Regras de anomalia (Strategy)
│   │       ├── Sync/             #      IRegraSincrona  → bloqueiam na hora
│   │       └── Async/            #      IRegraAssincrona → analisadas pelo Worker
│   │
│   ├── Vero.Application/         # 🎬 Casos de uso — orquestra o Domain
│   │   ├── Interfaces/           #    ITransacaoService, IReavaliacaoService (o que a Api pode chamar)
│   │   ├── Models/               #    NovaTransacao (entrada) e Resultado* (saída)
│   │   ├── Services/             #    TransacaoService, ReavaliacaoService, AnomalyDetectionService, CalculadoraDeRisco
│   │   └── DependencyInjection.cs#    AddVeroApplication() / AddVeroAnaliseAssincrona()
│   │
│   ├── Vero.Infrastructure/      # 🔌 Detalhes técnicos — implementa as interfaces do Domain
│   │   ├── Data/                 #    DbContext, migrations, seeder
│   │   ├── Repositories/         #    EF Core
│   │   ├── Queue/ Notifications/ #    Fila em memória, alerta no console (dev)
│   │   ├── Aws/                  #    SqsFilaTransacao, SnsAlertaService (produção)
│   │   └── DependencyInjection.cs#    AddVeroInfrastructure() / AddVeroInfrastructureEmMemoria()
│   │
│   ├── Vero.ML/                  # 🤖 Modelo de risco (ML.NET) — implementa IRiskScoringService
│   │
│   ├── Vero.Api/                 # 🌐 Entrega HTTP — só traduz HTTP ↔ Application
│   │   ├── Controllers/          #    Finos: validam, chamam o service, devolvem status HTTP
│   │   ├── DTOs/                 #    Formato do JSON de entrada/saída
│   │   ├── Mappers/              #    DTO → NovaTransacao, enum → texto
│   │   ├── Hubs/                 #    SignalR (implementa INotificadorTransacao)
│   │   ├── Middleware/           #    HMAC, rate limiting
│   │   └── Program.cs            #    Composition Root
│   │
│   └── Vero.Worker/              # ⚙️ Consome a fila (BackgroundService ou Lambda)
├── simulator/Vero.Simulator/     # Dispara transações fake
├── dashboard/                    # Painel React
├── docs/                         # 📚 Guia de estudo, um arquivo por assunto
├── tests/Vero.Tests/             # Testes unitários (xUnit + Moq)
└── infra/terraform/              # Infraestrutura como código
```

### Quem pode depender de quem

```
        Vero.Api ──────┐        Vero.Worker
           │           │             │
           ▼           ▼             ▼
   Vero.Application   Vero.Infrastructure   Vero.ML
           │                 │                 │
           └────────►  Vero.Domain  ◄──────────┘
```

A seta aponta sempre **para dentro**, em direção ao Domain. O Domain não
referencia nenhum outro projeto — ele só declara interfaces, e as camadas de
fora implementam. Se um dia você se pegar escrevendo `using Vero.Api` dentro
do Domain ou da Application, pare: tem algo no lugar errado.

## Como rodar localmente

> 🔐 **Nenhuma senha ou chave mora no código.** Tudo vem de variáveis de
> ambiente (veja o `.env.example`). Se alguma obrigatória faltar, a aplicação
> **não sobe** e diz exatamente o que está faltando — de propósito: um valor
> padrão silencioso faria a produção rodar com credenciais públicas.

### Jeito mais rápido (sem banco nenhum)

```bash
# Qualquer valor serve, desde que seja o MESMO na API e no simulador
export VERO_HMAC_SECRET=um-segredo-local-qualquer

# Terminal 1 — API com SQLite em memória e ~300 transações de exemplo
dotnet run --project src/Vero.Api -- --dev

# Terminal 2 — simulador disparando transações (mesmo VERO_HMAC_SECRET)
dotnet run --project simulator/Vero.Simulator -- --continuous --tps 2

# Terminal 3 — dashboard em http://localhost:5173
cd dashboard && npm install && npm run dev
```

### Modo completo (PostgreSQL)

Pré-requisitos: .NET 10 SDK e PostgreSQL rodando em `localhost:5432`.

1. Clone o repositório:
   ```bash
   git clone https://github.com/chequinato/vero-webhook.git
   cd vero-webhook
   ```

2. Crie o seu `.env` a partir do exemplo e preencha **a senha do banco e as
   chaves** (o `.env` está no `.gitignore` e nunca vai para o GitHub):
   ```bash
   cp .env.example .env
   ```

3. Carregue as variáveis no terminal (repita em cada terminal novo):
   ```bash
   export $(grep -v '^#' .env | xargs)
   ```

4. Aplique as migrations e rode a API e o Worker (cada um em um terminal):
   ```bash
   dotnet ef database update --project src/Vero.Infrastructure --startup-project src/Vero.Api
   dotnet run --project src/Vero.Api
   dotnet run --project src/Vero.Worker
   ```

## Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/transactions` | Recebe uma transação via webhook |
| `GET` | `/transactions/{id}` | Consulta status de uma transação |
| `GET` | `/transactions?suspicious=true` | Lista transações suspeitas |
| `GET` | `/health` | Healthcheck |
| `GET` | `/api/dashboard/stats` | Agregados da janela corrente |
| `GET` | `/api/dashboard/transactions` | Listagem paginada (`page`, `size` ≤ 100, `status`) |
| `GET` | `/api/dashboard/timeline` | Volume por hora nas últimas 24h |
| `GET` | `/api/dashboard/ml/metrics` | Estado e features do modelo |
| `POST` | `/api/dashboard/transactions/{id}/reavaliar` | Submete uma transação em revisão ao modelo |

### Reavaliação sob demanda

`POST /api/dashboard/transactions/{id}/reavaliar` roda o modelo de novo sobre
uma transação parada em revisão e resolve o caso: score no corte ou acima vira
`bloqueada`, abaixo vira `aprovada`. O corte é `0.70`, declarado em
`PoliticaDeRisco.LimiarBloqueio` (`src/Vero.Domain/Policies/`) — o mesmo número que o painel imprime
sobre a distribuição de risco.

Não existe decisão manual: o operador dispara a avaliação, quem decide é o
modelo. Transações já decididas devolvem **409**, para o painel não conseguir
desfazer um bloqueio por fora de um fluxo próprio; id inexistente devolve
**404**. O desfecho é persistido junto com o score novo numa transação de
banco só, e anunciado no hub como `StatusAtualizado`.

> ⚠️ As rotas `/api/dashboard/*` **não passam pelo middleware HMAC** — ele
> cobre apenas o webhook `POST /transactions`. Isso era inofensivo enquanto o
> dashboard só lia; a reavaliação muta estado. Antes de expor este host fora
> da rede interna, a rota precisa de autenticação de operador.

## Regras de anomalia

### Síncronas (bloqueiam na hora)
- Valor acima de R$100.000
- Score de confiança da conta abaixo de 35

### Assíncronas (análise em background)
- Velocity: múltiplas transações do mesmo remetente em 5 minutos
- Horário estranho: transações entre 1h e 5h
- Valor redondo: múltiplos de R$1.000 acima de R$1.000

## Segurança

| Camada | Descrição |
|--------|-----------|
| **HMAC** | Toda requisição POST ao webhook precisa do header `X-Signature` com a assinatura HMAC-SHA256 do body. Comparação em tempo constante (protege contra timing attacks). |
| **Rate Limiting** | Sliding window por IP — padrão: 60 requisições por minuto. Configurável via `appsettings.json`. |
| **Replay Protection** | O `id` da transação é único; transações reenviadas recebem `409 Conflict`. |
| **Criptografia em repouso** | Dados sensíveis (`NumeroConta`, `Titular`) são criptografados com AES-256-CBC no banco via EF Core Value Converter. |
| **Segredos fora do código** | Senha do banco, chave HMAC e chave de criptografia vêm só de variáveis de ambiente. Não existe valor padrão no código: se faltar, a aplicação falha ao subir em vez de usar uma credencial conhecida. |

## Testes

```bash
dotnet test Vero.slnx
```

**113 testes** cobrindo:

| Pasta | O que testa |
|-------|-------------|
| `Domain/` (32) | Regras síncronas e assíncronas, `PoliticaDeRisco` (sem mock nenhum!) |
| `Application/` (30) | `TransacaoService`, `ReavaliacaoService`, `AnomalyDetectionService` com mocks |
| `Api/` (9) | `TransactionsController` — só traduz resultado em status HTTP |
| `Security/` (17) | AES-256-CBC, HMAC-SHA256, rate limiting |
| `ML/` (14) | Gerador de dados, treino e scoring |
| `Infrastructure/` (7) | `TransacaoRepository` com EF Core InMemory |
| `CompositionRootTests` (4) | Os métodos `Add*` montam o container de DI da Api e do Worker; sem configuração, a infraestrutura recusa subir |

## Infraestrutura AWS (Terraform)

Todos os serviços ficam dentro do **Always Free Tier** da AWS:

| Recurso | Serviço | Free Tier |
|---------|---------|-----------|
| Fila de análise | SQS | 1M requisições/mês |
| Dead Letter Queue | SQS | incluído |
| Alertas | SNS | 1K notificações email/mês |
| Worker | Lambda | 1M invocações + 400K GB-s/mês |
| Banco de dados | RDS PostgreSQL | ⚠️ **NÃO aplicar** — custa fora do Free Tier |

### Deploy da infraestrutura

```bash
cd infra/terraform
terraform init
terraform plan    # verificar sempre antes de aplicar
terraform apply   # aplica SQS + SNS + Lambda (NÃO aplica RDS)
```

> ⚠️ O `rds.tf` existe apenas como referência. **Nunca** execute `terraform apply` nele
> sem verificar custos. Está marcado com tag `NAO-APLICAR-SEM-FREE-TIER`.

## Arquitetura

```
Client → [POST /transactions] → HMAC + Rate Limit → Controller
                                                         │
                              ┌─────────────────────────┤
                              │ Sync Rules              │
                              │ (valor alto, score)     │
                              │                         │
                        Bloqueada?                Aceita provisória
                         → 403                         │
                                                       ▼
                                              Persiste no DB (EF Core)
                                                       │
                                                       ▼
                                              Enfileira no SQS
                                                       │
                                                       ▼
                                              Lambda / Worker
                                                       │
                                              Async Rules
                                              (velocity, horário, valor redondo)
                                                       │
                                          ┌────────────┴────────────┐
                                     Suspeita                   Aprovada
                                    → SNS alert               → status update
```

Em código, o caminho de um `POST /transactions` é:

```
TransactionsController           (Api)     valida o DTO e converte em NovaTransacao
  └─ TransacaoService            (App)     orquestra tudo abaixo
       ├─ IContaRepository       (Domain)  resolve remetente/destinatário
       ├─ ITransacaoRepository   (Domain)  checa replay
       ├─ CalculadoraDeRisco     (App)     velocity + IRiskScoringService
       ├─ IRegraSincrona[]       (Domain)  primeira regra violada → bloqueia
       ├─ IFilaTransacao         (Domain)  manda para o Worker
       └─ INotificadorTransacao  (Domain)  avisa o dashboard
  └─ devolve ResultadoRecebimento → controller traduz em 202 / 403 / 409 / 400
```
