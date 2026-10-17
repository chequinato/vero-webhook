# 03 — Clean Architecture

> **🧠 Pra gravar**
> - **Negócio no centro, ferramenta na borda. A seta de dependência aponta para dentro.**
> - O Domain **não referencia nenhum projeto**: só declara interfaces; as camadas de fora implementam.
> - A classe **recebe** o que precisa pelo construtor (injeção de dependência) em vez de dar `new`.
> - O `Program.cs` é o **Composition Root**: o único lugar que conhece todas as camadas.

## A ideia

Separar o código em camadas concêntricas em que **as internas não conhecem as externas**.

```
┌──────────────────────────────────────────────┐
│              Api / Worker (borda)             │
│  ┌────────────────────────────────────────┐  │
│  │        Infrastructure / ML              │  │
│  │  ┌──────────────────────────────────┐  │  │
│  │  │         Application              │  │  │
│  │  │  ┌────────────────────────────┐  │  │  │
│  │  │  │     Domain (núcleo)        │  │  │  │
│  │  │  └────────────────────────────┘  │  │  │
│  │  └──────────────────────────────────┘  │  │
│  └────────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

| Camada | Pergunta que responde | Exemplo no Vero |
|---|---|---|
| **Domain** | *O que* é o negócio? | `Transacao`, `ValorAltoRule`, `PoliticaDeRisco` |
| **Application** | *Em que ordem* as coisas acontecem? | `TransacaoService` |
| **Infrastructure** | *Com que ferramenta*? | EF Core, SQS, SNS, AES |
| **Api / Worker** | *Por onde* o pedido chega? | Controller (HTTP), Worker (fila) |

## Quem referencia quem (`.csproj`)

```
Vero.Domain          → ninguém
Vero.Application     → Domain
Vero.Infrastructure  → Domain
Vero.ML              → Domain
Vero.Api             → Application + Infrastructure + Domain + ML
Vero.Worker          → Application + Infrastructure + Domain
```

```
        Vero.Api ──────┐        Vero.Worker
           │           │             │
           ▼           ▼             ▼
   Vero.Application   Vero.Infrastructure   Vero.ML
           │                 │                 │
           └────────►  Vero.Domain  ◄──────────┘
```

Se um dia você escrever `using Vero.Api` dentro do Domain ou da Application,
pare: alguma coisa está no lugar errado.

## Por que isso importa

- O **Domain** não sabe que banco ou HTTP existem. Ele diz *o que* precisa
  (`ITransacaoRepository`), não *como*.
- A **Application** orquestra usando interfaces. Não sabe se o banco é
  PostgreSQL ou SQLite.
- A **Infrastructure** implementa essas interfaces.
- A **Api** é o ponto de entrada que monta tudo.

Trocar PostgreSQL por MongoDB = mexer só na Infrastructure. E dá para testar
as regras de negócio **sem banco, sem AWS e sem HTTP**.

## Interfaces: o contrato

```csharp
// Domain declara o que precisa
public interface ITransacaoRepository
{
    Task<Transacao?> ObterPorIdAsync(string id);
    Task AdicionarAsync(Transacao transacao);
    // ...
}

// Infrastructure cumpre
public class TransacaoRepository : ITransacaoRepository { ... }
```

**Analogia:** a interface é o cardápio; a implementação é a cozinha. A cozinha
muda sem reimprimir o cardápio.

### Onde mora cada interface

| Tipo de interface | Onde mora | Exemplos |
|---|---|---|
| O que o negócio precisa do mundo de fora | `Vero.Domain/Interfaces` | `ITransacaoRepository`, `IFilaTransacao`, `IAlertaService`, `INotificadorTransacao` |
| O que a Api pode chamar | `Vero.Application/Interfaces` | `ITransacaoService`, `IReavaliacaoService` |

`ITransacaoService` morava no Domain. Foi para a Application porque o Domain
não deveria saber que existem "serviços de aplicação".

## Injeção de dependência (DI)

```csharp
// ❌ Sem DI — acoplado: só funciona com esse repositório e esse banco
public class TransacaoService
{
    private readonly TransacaoRepository _repo = new(new VeroDbContext());
}

// ✅ Com DI — recebe contratos; pode ser EF Core, pode ser mock
public class TransacaoService : ITransacaoService
{
    public TransacaoService(
        ITransacaoRepository transacaoRepository,
        IContaRepository contaRepository,
        IFilaTransacao filaTransacao,
        CalculadoraDeRisco calculadoraDeRisco,
        INotificadorTransacao notificador,
        IEnumerable<IRegraSincrona> regrasSincronas) { ... }
}
```

## Composition Root: um `Add*` por camada

Cada camada tem um `DependencyInjection.cs` que registra as próprias peças.
O `Program.cs` só escolhe quais montar:

```csharp
// src/Vero.Api/Program.cs
builder.Services.AddVeroInfrastructure(connectionString, chave);  // banco, repos, fila, alertas
builder.Services.AddVeroMl(caminhoModelo);                        // modelo de risco
builder.Services.AddVeroApplication();                            // services + regras
builder.Services.AddSingleton<INotificadorTransacao, TransactionHubNotifier>();
```

O Worker e a Lambda usam os **mesmos** métodos, mas só o que precisam:

```csharp
// src/Vero.Worker/Program.cs
builder.Services.AddVeroInfrastructure(...);
builder.Services.AddVeroAnaliseAssincrona();   // só regras assíncronas + AnomalyDetectionService
```

Antes, `Api/Program.cs`, `Worker/Program.cs` e `LambdaHandler.cs` copiavam as
mesmas linhas de registro — e já tinham divergido. O teste
`CompositionRootTests` garante que esses métodos montam um container completo.

### Registrar várias implementações da mesma interface

```csharp
services.AddScoped<IRegraSincrona, ValorAltoRule>();
services.AddScoped<IRegraSincrona, ScoreBaixoRule>();
// Quem pedir IEnumerable<IRegraSincrona> recebe as duas, nessa ordem
```

### Ciclos de vida

| Tipo | Instância | No Vero |
|---|---|---|
| **Transient** | Nova a cada injeção | — |
| **Scoped** | Uma por requisição HTTP | Repositórios (usam `DbContext`), services, regras |
| **Singleton** | Uma para a aplicação inteira | Modelo de ML, fila em memória, notificador SignalR |

> **Armadilha:** um singleton não pode depender de algo scoped. Ele viveria
> para sempre segurando um `DbContext` de uma requisição que já acabou. Em
> Development o .NET recusa subir se isso acontecer (`ValidateScopes`).

---

**Anterior:** [← 02 — C#](02-csharp.md) · **Próximo:** [04 — SOLID e orientação a objetos →](04-solid-e-poo.md)
