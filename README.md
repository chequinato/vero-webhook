# Vero — API de Webhooks com Detecção de Anomalias em Tempo Real

API que recebe transações financeiras via webhook e decide, em tempo real, se
a transação é confiável, suspeita ou deve ser bloqueada imediatamente.

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
│   ├── Vero.Api/              # Controllers, DTOs, Middleware
│   ├── Vero.Domain/           # Entidades, Enums, Regras, Interfaces
│   ├── Vero.Application/      # Services (orquestração)
│   ├── Vero.Infrastructure/   # Repositórios, DbContext, AWS clients
│   └── Vero.Worker/           # Worker que consome a fila
├── simulator/
│   └── Vero.Simulator/        # Dispara transações fake
├── tests/
│   └── Vero.Tests/            # Testes unitários (xUnit)
└── infra/
    └── terraform/             # Infraestrutura como código
```

## Como rodar localmente

### Pré-requisitos

- .NET 8+ SDK
- PostgreSQL rodando em `localhost:5432`

### Setup

1. Clone o repositório:
   ```bash
   git clone https://github.com/chequinato/vero-webhook.git
   cd vero-webhook
   ```

2. Configure a connection string (copie `.env.example` para `.env` e ajuste):
   ```bash
   cp .env.example .env
   ```

3. Aplique as migrations:
   ```bash
   dotnet ef database update --project src/Vero.Infrastructure --startup-project src/Vero.Api
   ```

4. Rode a API:
   ```bash
   dotnet run --project src/Vero.Api
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
`DashboardController.LimiarBloqueio` — o mesmo número que o painel imprime
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
| **Segredos fora do código** | Todas as chaves e credenciais vêm de variáveis de ambiente ou `appsettings`, nunca hardcoded. |

## Testes

```bash
dotnet test
```

**76 testes** cobrindo:

### Domain (24 testes)
- Regras síncronas: ValorAlto (3), ScoreBaixo com mock (5)
- Regras assíncronas: HorarioEstranho (6), ValorRedondo (6), Velocity com mock (4)

### Application (19 testes)
- TransacaoService: fluxo normal, bloqueio, replay, múltiplas regras, consulta, listagem (11)
- AnomalyDetectionService: aprovação, suspeita+alerta, não encontrada, status inválido (8)

### API (8 testes)
- TransactionsController: POST 202/403/409/400, GET por id, GET 404, GET suspeitas, GET sem flag

### Security (15 testes)
- AES-256-CBC: encrypt/decrypt, IV aleatório, chave errada, edge cases (8)
- HMAC-SHA256: determinismo, payloads diferentes, chaves diferentes (4)
- Rate limiting: sliding window, expiração (3)

### Infrastructure (7 testes)
- TransacaoRepository: adicionar, histórico, exists, update status, listar suspeitas, contagem por período

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
