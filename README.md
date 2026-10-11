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

### Jeito mais rápido (sem banco nenhum)

```bash
dotnet run --project src/Vero.Api -- --dev
```

O `--dev` sobe a API com SQLite em memória e já popula ~300 transações de
exemplo. Ideal para estudar e brincar com o dashboard.

### Pré-requisitos (modo completo)

- .NET 10 SDK
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
| **Segredos fora do código** | Todas as chaves e credenciais vêm de variáveis de ambiente ou `appsettings`, nunca hardcoded. |

## Testes

```bash
dotnet test Vero.slnx
```

**112 testes** cobrindo:

| Pasta | O que testa |
|-------|-------------|
| `Domain/` (32) | Regras síncronas e assíncronas, `PoliticaDeRisco` (sem mock nenhum!) |
| `Application/` (30) | `TransacaoService`, `ReavaliacaoService`, `AnomalyDetectionService` com mocks |
| `Api/` (9) | `TransactionsController` — só traduz resultado em status HTTP |
| `Security/` (17) | AES-256-CBC, HMAC-SHA256, rate limiting |
| `ML/` (14) | Gerador de dados, treino e scoring |
| `Infrastructure/` (7) | `TransacaoRepository` com EF Core InMemory |
| `CompositionRootTests` (3) | Os métodos `Add*` montam um container de DI completo para a Api e para o Worker |

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

---

# 📚 Guia de estudo: Arquitetura, SOLID e Design Patterns

Este projeto foi organizado para servir de material de estudo. Cada conceito
abaixo aponta para o arquivo onde ele aparece de verdade, e quase todo arquivo
importante tem um comentário explicando **por que** ele existe e **como era
antes**.

**Como usar:** antes de abrir o arquivo de cada seção, leia a pergunta 🤔 e
tente responder sozinho(a). Só depois confira no código. Errar o palpite e
descobrir o porquê ensina mais do que ler a resposta pronta.

## 1. Clean Architecture — cada coisa no seu lugar

A ideia central: **regra de negócio não pode depender de detalhe técnico.**
Banco, HTTP, SignalR, AWS, ML.NET são detalhes. "Valor acima de R$100.000 é
bloqueado" é negócio.

| Camada | Pergunta que ela responde | Exemplo no Vero |
|--------|---------------------------|-----------------|
| **Domain** | *O que* é o negócio? | `Transacao`, `ValorAltoRule`, `PoliticaDeRisco` |
| **Application** | *Em que ordem* as coisas acontecem? | `TransacaoService` (resolve conta → score → regras → salva → fila) |
| **Infrastructure** | *Com que ferramenta*? | `TransacaoRepository` (EF Core), `SqsFilaTransacao` (AWS) |
| **Api / Worker** | *Por onde* o pedido chega? | `TransactionsController` (HTTP), `AsyncWorker` (fila) |

> 🤔 O `TransacaoService` precisa salvar no banco, mas o projeto
> `Vero.Application` **não referencia** `Vero.Infrastructure`. Como ele salva?
> (Dica: abra `ITransacaoRepository` e veja em que projeto ele mora.)

**Composition Root** — alguém precisa ligar a interface à implementação. Esse
alguém é o `Program.cs` de cada executável. Ele é o único lugar que conhece
todas as camadas, e ele só chama um método por camada:

```csharp
builder.Services.AddVeroInfrastructure(...);   // Vero.Infrastructure/DependencyInjection.cs
builder.Services.AddVeroMl(...);               // Vero.ML/DependencyInjection.cs
builder.Services.AddVeroApplication();         // Vero.Application/DependencyInjection.cs
```

Antes, `Api/Program.cs`, `Worker/Program.cs` e `LambdaHandler.cs` copiavam as
mesmas 15 linhas de registro, e já tinham divergido (a Api injetava regras
**síncronas** no `AnomalyDetectionService` sem ninguém perceber).

## 2. SOLID

### S — Single Responsibility (uma razão para mudar)

Uma classe deve ter **um único motivo** para ser alterada.

- **Antes:** `TransactionsController` resolvia contas, contava velocity,
  chamava o ML, chamava o service, montava payload do SignalR e escolhia o
  status HTTP. Seis motivos para mudar. O teste dele precisava de 5 mocks.
- **Depois:** o controller só traduz HTTP ↔ Application
  (`src/Vero.Api/Controllers/TransactionsController.cs`). O teste precisa de 1
  mock. A orquestração foi para `TransacaoService`, e a decisão do corte de 70%
  saiu do `DashboardController` para `src/Vero.Domain/Policies/PoliticaDeRisco.cs`.

> 🤔 Compare `tests/Vero.Tests/Domain/PoliticaDeRiscoTests.cs` com
> `tests/Vero.Tests/Api/TransactionsControllerTests.cs`. Qual é mais simples
> de escrever? O que isso diz sobre onde a regra deveria morar?

### O — Open/Closed (aberto para extensão, fechado para modificação)

Dá para **adicionar comportamento sem mexer no código que já funciona**.

- Para criar uma regra nova de fraude você cria uma classe que implementa
  `IRegraSincrona` (ou `IRegraAssincrona`) e adiciona **uma linha** em
  `src/Vero.Application/DependencyInjection.cs`. O `TransacaoService` não muda
  nada — ele recebe `IEnumerable<IRegraSincrona>` e percorre o que vier.

### L — Liskov Substitution (substituto não pode surpreender)

Qualquer implementação de uma interface tem que poder entrar no lugar de outra
**sem quebrar quem usa**.

- `InMemoryFilaTransacao` e `SqsFilaTransacao` implementam `IFilaTransacao`.
  As duas devolvem `null` quando a fila está vazia. Se a do SQS lançasse
  exceção nesse caso, o `AsyncWorker` quebraria só em produção — isso seria
  uma violação de Liskov.

> 🤔 Abra `IFilaTransacao`. O comentário diz o que acontece quando a fila está
> vazia. Por que documentar isso no **contrato** e não só na implementação?

### I — Interface Segregation (interfaces pequenas e específicas)

Ninguém deve ser obrigado a depender de métodos que não usa.

- **Antes:** toda regra era `IRule`, e quem decidia se ela era síncrona ou
  assíncrona era a ordem de registro no `Program.cs`. Registrar errado = regra
  pesada rodando no caminho do webhook.
- **Depois:** `IRegraSincrona` e `IRegraAssincrona` (`src/Vero.Domain/Rules/`).
  O `TransacaoService` pede `IEnumerable<IRegraSincrona>` e o compilador garante
  que só chegam regras síncronas.
- Também: `ITransacaoService` e `IReavaliacaoService` são separados — o
  `DashboardController` não precisa conhecer o recebimento de webhook.

> 🤔 `ITransacaoRepository` tem 10 métodos, e o dashboard só usa 3 deles. Isso
> é uma violação de ISP? (Veja o exercício 3 lá embaixo.)

### D — Dependency Inversion (dependa de abstrações)

Código de alto nível (regras, orquestração) não depende de código de baixo
nível (SignalR, EF, AWS). **Os dois dependem de uma interface.**

- `TransacaoService` precisa avisar o dashboard. Ele **não** conhece SignalR:
  conhece `INotificadorTransacao` (Domain). Quem implementa é
  `TransactionHubNotifier` (Api). No teste, vira um `Mock<INotificadorTransacao>`.
- Mesma coisa para `IRiskScoringService` (ML.NET), `IAlertaService` (console
  ou SNS), `IFilaTransacao` (memória ou SQS).

> 🤔 Se amanhã o dashboard trocar SignalR por WebSocket puro, quais arquivos
> mudam? E quais **não** mudam?

**Contraponto importante:** nem tudo precisa de interface. `CalculadoraDeRisco`
é uma classe concreta, sem interface, porque não existe motivo real para ter
duas versões dela — e ela já é testável pelas dependências que recebe.
Interface sem motivo é cerimônia.

## 3. Design Patterns que aparecem no projeto

| Pattern | Onde | Para quê |
|---------|------|----------|
| **Strategy** | `IRule` + `Rules/Sync/*`, `Rules/Async/*` | Cada regra é uma estratégia intercambiável; o service não sabe quais existem |
| **Repository** | `ITransacaoRepository`, `IContaRepository` | Esconde o EF Core: a Application pensa em "coleção de transações", não em SQL |
| **Adapter** | `SqsFilaTransacao`, `SnsAlertaService`, `TransactionHubNotifier`, `MlRiskScoringService` | Adapta uma biblioteca externa (AWS SDK, SignalR, ML.NET) à interface que o Domain pediu |
| **Observer** | `INotificadorTransacao` | O service "publica" que algo aconteceu; quem estiver ouvindo (dashboard) reage |
| **Result** | `ResultadoRecebimento`, `ResultadoReavaliacao` | Casos esperados ("conta não existe", "duplicada") viram valores, não exceções nem tuplas soltas |
| **Mapper** | `src/Vero.Api/Mappers/TransacaoMapper.cs` | Um lugar só converte DTO ↔ modelo e enum ↔ texto |
| **Chain of Responsibility** | Pipeline de middlewares no `Api/Program.cs` | Rate limit → HMAC → controller; cada elo pode barrar a requisição |
| **Dependency Injection** | `DependencyInjection.cs` de cada camada | Ninguém dá `new` em dependência; recebe pelo construtor |
| **Policy** | `PoliticaDeRisco` | Regra de decisão isolada, pura e testável |

### Idiomas de C# que valem a pena reparar

- **Extension methods** — `services.AddVeroApplication()`,
  `regras.PrimeiraVioladaAsync(transacao)`, `status.ParaTexto()`. Métodos
  estáticos que "parecem" métodos da classe.
- **`record`** — `NovaTransacao`, `ResultadoRecebimento`. Imutável, com
  igualdade por valor e `with` para criar cópias (veja o teste
  `ProcessarTransacao_ContaInexistente_NaoDevePersistir`).
- **`switch` expression** — `TransactionsController.ReceberTransacao` traduz
  cada `DesfechoRecebimento` em um status HTTP.
- **Interface vazia herdando outra** — `public interface IRegraSincrona : IRule;`

## 4. Checklist de hábitos (antes de abrir um PR)

1. **Em que camada isso mora?** Regra de negócio → Domain. Orquestração →
   Application. Ferramenta → Infrastructure. HTTP → Api.
2. **Meu controller tem `if` de negócio?** Se sim, mova para a Application ou
   Domain.
3. **Minha classe recebe mais de ~5 dependências?** Provavelmente faz coisa
   demais (SRP).
4. **Dei `new` em algo de infraestrutura dentro de um service?** Troque por uma
   interface injetada (DIP).
5. **Escrevi um número mágico?** (`5`, `0.70f`...) Dê nome a ele e deixe no
   lugar que é "dono" da regra.
6. **Copiei e colei um bloco?** Extraia (DRY) — o `AddMinutes(-5)` repetido
   em dois controllers é o exemplo clássico deste projeto.
7. **Registrei a peça nova no `DependencyInjection.cs` da camada certa?**
   O `CompositionRootTests` vai te avisar se esqueceu.
8. **Escrevi o teste?** Se for difícil testar, geralmente o design está
   pedindo para ser separado.

## 5. Exercícios práticos

Faça na ordem. Para cada um, antes de codar, escreva num papel:
**qual problema? qual entrada? qual saída? quais passos? dá para quebrar em
partes menores?** As dicas estão escondidas — abra uma de cada vez, só se travar.

### Exercício 1 — Nova regra síncrona (Strategy + Open/Closed)

Bloquear transações cuja `Moeda` não seja `"BRL"`.

<details><summary>Dica 1</summary>

Olhe `ValorAltoRule`. Sua regra é muito parecida.
</details>

<details><summary>Dica 2</summary>

Ela implementa `IRegraSincrona`, mora em `src/Vero.Domain/Rules/Sync/` e
precisa de **uma** linha em `src/Vero.Application/DependencyInjection.cs`.
</details>

<details><summary>Dica 3</summary>

Quantos arquivos **existentes** você precisou alterar além do
`DependencyInjection.cs`? Se a resposta for zero, você acabou de ver o
Open/Closed funcionando. Agora escreva o teste em `tests/Vero.Tests/Domain/`.
</details>

### Exercício 2 — Controlar o tempo nos testes (Dependency Inversion)

`TransacaoService` usa `DateTime.UtcNow` direto. Como testar que a janela de
velocity termina "agora" se "agora" muda a cada execução?

<details><summary>Dica 1</summary>

`DateTime.UtcNow` é uma dependência escondida — igual a dar `new` num
repositório.
</details>

<details><summary>Dica 2</summary>

O .NET já tem uma abstração para isso: `TimeProvider`. Injete no construtor e
use `_timeProvider.GetUtcNow()`.
</details>

<details><summary>Dica 3</summary>

Em produção registre `TimeProvider.System`. No teste, crie uma subclasse de
`TimeProvider` que devolve uma data fixa (ou use o pacote
`Microsoft.Extensions.TimeProvider.Testing` com `FakeTimeProvider`).
</details>

### Exercício 3 — Separar leitura de escrita (Interface Segregation)

As rotas de leitura do `DashboardController` só consultam estatísticas, mas ele
depende do `ITransacaoRepository` inteiro (que também grava).

<details><summary>Dica 1</summary>

Liste quais métodos do repositório cada classe usa de verdade. Os grupos que
aparecerem são suas interfaces novas.
</details>

<details><summary>Dica 2</summary>

Crie algo como `IDashboardQueries` com `ObterEstatisticasAsync`,
`ListarPaginadoAsync` e `ObterVolumePorHoraAsync`. A mesma classe
`TransacaoRepository` pode implementar as duas interfaces.
</details>

### Exercício 4 — Escolher a implementação pela configuração (Factory)

Em produção o alerta deveria ir pelo SNS (`SnsAlertaService`); em dev, pelo
console. Hoje está fixo no console.

<details><summary>Dica 1</summary>

O lugar para decidir isso é o `src/Vero.Infrastructure/DependencyInjection.cs`.
Nenhum service deve saber qual implementação está rodando.
</details>

<details><summary>Dica 2</summary>

`services.AddSingleton<IAlertaService>(sp => ...)` aceita uma função que
**fabrica** a instância. Dentro dela você pode olhar uma variável de ambiente
e devolver uma ou outra.
</details>

### Exercício 5 — Log em volta de qualquer alerta (Decorator)

Quero registrar no log quanto tempo cada alerta demorou, seja console ou SNS,
**sem alterar** nenhuma das duas classes.

<details><summary>Dica 1</summary>

Crie uma classe que implementa `IAlertaService` **e recebe** outro
`IAlertaService` no construtor.
</details>

<details><summary>Dica 2</summary>

No `EnviarAlertaAsync`, ela inicia um `Stopwatch`, chama o alerta "de dentro"
e loga o tempo. Isso é o Decorator: mesma interface por fora, comportamento
extra em volta.
</details>

### Pergunta bônus (sem código)

A Api e o Worker registram, cada um, um `InMemoryFilaTransacao`. Se você rodar
os dois localmente, a transação que a Api enfileira chega no Worker? Por quê?
O que muda quando se usa o `SqsFilaTransacao`?

## 6. Glossário rápido

| Termo | Em uma frase |
|-------|--------------|
| **DTO** | Objeto que só carrega dados entre a API e o mundo de fora (JSON) |
| **Entidade** | Objeto do negócio com identidade (`Transacao` tem `Id`) |
| **Caso de uso** | Uma ação completa que o sistema oferece (receber transação, reavaliar) |
| **Porta (interface)** | Contrato que o núcleo declara para falar com o mundo de fora |
| **Adapter** | Implementação concreta de uma porta usando uma ferramenta específica |
| **Composition Root** | O único lugar que monta o grafo de objetos (o `Program.cs`) |
| **Mock** | Objeto falso usado no teste no lugar de uma dependência real |
| **YAGNI** | "You Aren't Gonna Need It" — não crie abstração antes de precisar |
| **DRY** | "Don't Repeat Yourself" — uma regra, um lugar |
