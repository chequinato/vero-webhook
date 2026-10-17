# 05 — Design patterns

> **🧠 Pra gravar**
> - **Strategy**: cada regra é uma peça trocável · **Repository**: a Application pensa em "coleção", não em SQL · **Adapter**: biblioteca externa vestida com a interface do Domain.
> - **Observer**: quem produz o evento não conhece quem escuta · **Result**: desfecho esperado vira valor, não exceção.
> - Pattern é vocabulário: serve para dar nome a uma solução, não para ser colecionado.

| Pattern | Onde no Vero | Para quê |
|---|---|---|
| **Strategy** | `IRule` + `Rules/Sync/*`, `Rules/Async/*` | Comportamentos intercambiáveis atrás de uma interface |
| **Repository** | `ITransacaoRepository`, `IContaRepository` | Esconder o EF Core da Application |
| **Adapter** | `SqsFilaTransacao`, `SnsAlertaService`, `TransactionHubNotifier`, `MlRiskScoringService` | Adaptar AWS SDK, SignalR e ML.NET à interface do Domain |
| **Observer** | `INotificadorTransacao`, `engine/bus.ts` | Desacoplar quem produz evento de quem consome |
| **Result** | `ResultadoRecebimento`, `ResultadoReavaliacao` | Desfechos esperados como valores |
| **Mapper** | `Api/Mappers/TransacaoMapper.cs` | Um lugar só converte DTO ↔ modelo |
| **Policy** | `Domain/Policies/PoliticaDeRisco.cs` | Regra de decisão pura, isolada e testável |
| **Chain of Responsibility** | Pipeline de middlewares | Cada elo pode barrar a requisição |
| **Dependency Injection** | `DependencyInjection.cs` de cada camada | Ninguém dá `new` em dependência |

## Strategy

Sem ele, seria um `if / else if` gigante com todas as regras dentro do service.

```csharp
public interface IRule
{
    string Nome { get; }                       // vira o "motivo" da transação
    Task<bool> Avaliar(Transacao transacao);   // true = violou
}

public interface IRegraSincrona : IRule;       // rápidas: rodam no webhook
public interface IRegraAssincrona : IRule;     // pesadas: rodam no Worker
```

O loop que aplica as regras existe uma vez só, como extension method:

```csharp
// Domain/Rules/RegrasExtensions.cs
public static async Task<IRule?> PrimeiraVioladaAsync(
    this IEnumerable<IRule> regras, Transacao transacao)
{
    foreach (var regra in regras)
        if (await regra.Avaliar(transacao))
            return regra;

    return null;
}
```

Antes esse mesmo `foreach` estava copiado no `TransacaoService` e no
`AnomalyDetectionService` (DRY: *Don't Repeat Yourself*).

## Repository

A Application pede "a transação de id X" e não sabe se veio de PostgreSQL,
SQLite ou de um mock. Toda a sintaxe de EF Core fica confinada em
`Infrastructure/Repositories`.

## Adapter

Cada biblioteca externa tem a API dela; o Domain declarou a interface que quer.
O adapter faz a ponte:

```
IFilaTransacao.EnviarParaAnaliseAsync(id)
        ↓  SqsFilaTransacao
_sqs.SendMessageAsync(new SendMessageRequest { QueueUrl = ..., MessageBody = id })
```

O mesmo vale para `SnsAlertaService` (AWS SNS), `TransactionHubNotifier`
(SignalR) e `MlRiskScoringService` (ML.NET). Para ir de dev a produção, só o
registro em `Infrastructure/DependencyInjection.cs` muda.

## Observer

O service **publica** "uma transação chegou"; quem estiver ouvindo reage.

```csharp
public interface INotificadorTransacao
{
    Task TransacaoRecebidaAsync(Transacao transacao);
    Task StatusAtualizadoAsync(Transacao transacao);
}
```

No front-end, `engine/bus.ts` faz o mesmo para a cena 3D (veja
[12 — Estado ao vivo](12-estado-ao-vivo.md#barramento-de-eventos-fora-do-react)).

## Result

Em vez de lançar exceção para casos **esperados** ("conta não existe",
"transação repetida") ou devolver tuplas soltas, o service devolve um objeto
que diz o que aconteceu:

```csharp
public enum DesfechoRecebimento { Aceita, Bloqueada, Duplicada, ContaNaoEncontrada }

public record ResultadoRecebimento(DesfechoRecebimento Desfecho, Transacao? Transacao)
{
    public static ResultadoRecebimento Aceita(Transacao t) => new(DesfechoRecebimento.Aceita, t);
    public static ResultadoRecebimento ContaNaoEncontrada() => new(DesfechoRecebimento.ContaNaoEncontrada, null);
    // ...
}
```

O controller só traduz cada desfecho em status HTTP (veja o `switch` em
[02 — C#](02-csharp.md#switch-expression)).

**Regra prática:** exceção é para o **inesperado** (banco caiu). Desfecho de
negócio previsto é **valor de retorno**.

## Mapper

```csharp
// Api/Mappers/TransacaoMapper.cs
public static NovaTransacao ParaNovaTransacao(this TransacaoRequestDto dto) => new(...);
public static string ParaTexto(this StatusTransacao status) => status.ToString().ToLowerInvariant();
```

Antes, `Status.ToString().ToLowerInvariant()` aparecia espalhado por vários
lugares. Centralizar garante que a API fala o mesmo "idioma" em todas as rotas.

## Policy

```csharp
// Domain/Policies/PoliticaDeRisco.cs
public const float LimiarBloqueio = 0.70f;

public static bool PodeSerReavaliada(StatusTransacao status) =>
    status is StatusTransacao.Suspeita or StatusTransacao.AceitaProvisoria;

public static (StatusTransacao Status, string? Motivo) Decidir(float score) => ...;
```

Essa decisão morava dentro do `DashboardController`. Controller é entrega
HTTP: se amanhã a reavaliação vier de uma fila, a regra teria que ser copiada.
No Domain ela é pura e se testa **sem mock nenhum** (`PoliticaDeRiscoTests`).

## Chain of Responsibility

```
Requisição → CORS → Rate limiting → HMAC → Controller
                        ↓              ↓
                      429            401
```

Cada middleware decide: passo para o próximo (`await _next(context)`) ou
barro aqui? **A ordem de registro no `Program.cs` importa.**

---

**Anterior:** [← 04 — SOLID](04-solid-e-poo.md) · **Próximo:** [06 — As camadas na prática →](06-camadas.md)
