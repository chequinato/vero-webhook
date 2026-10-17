# 02 — C#, a linguagem

> **🧠 Pra gravar**
> - `decimal` para dinheiro · `?` para o que pode faltar · `await` em todo I/O · `record` para o que é só dado.
> - LINQ com EF Core vira SQL, e **só vai ao banco no `ToListAsync()`**.
> - Extension method = método estático que "gruda" num tipo pelo `this` do primeiro parâmetro.

Só os recursos que aparecem neste projeto, com o arquivo onde estão.

## 1. Tipos e `decimal`

C# é **fortemente tipada**: toda variável tem tipo definido em tempo de compilação.

```csharp
int score = 90;              // inteiro 32 bits
float riskScore = 0.85f;     // ponto flutuante (o 'f' é obrigatório)
decimal valor = 50_000.50m;  // decimal 128 bits (o 'm' é obrigatório)
string id = "txn_001";       // texto (imutável)
DateTime agora = DateTime.UtcNow;

decimal limite = 100_000m;   // o '_' só melhora a leitura
```

**Por que `decimal` para dinheiro?** `float` e `double` usam base 2 e não
representam `0.1` exatamente: acumulam erro de arredondamento. `decimal` usa
base 10. Veja `Transacao.Valor`.

## 2. Nullable reference types (`?`)

```csharp
string nome = "Maria";     // o compilador avisa se você tentar pôr null
string? motivo = null;     // o '?' diz: "pode ser null"
float? riskScore = null;   // tipos valor também
```

No projeto:

```csharp
// MlRiskScoringService.cs — conta pode não existir
var score = remetente?.Score ?? 50;
//                   ^^ só acessa .Score se remetente não for null
//                            ^^ se o resultado for null, usa 50
```

## 3. `async` / `await`

Enquanto espera o banco, a thread fica livre para atender outra requisição.

```csharp
var transacao = await _transacaoRepository.ObterPorIdAsync(id);
```

- Métodos async retornam `Task` (sem valor) ou `Task<T>` (com valor).
- **Esqueceu o `await`?** Compila, mas a operação roda "solta" e você não espera o resultado.
- Sem I/O de verdade, devolva uma `Task` pronta:

```csharp
// ValorAltoRule.cs — conta simples, nada a esperar
public Task<bool> Avaliar(Transacao transacao) =>
    Task.FromResult(transacao.Valor > LimiteValor);
```

## 4. Records

Imutáveis e comparados **por valor**. Perfeitos para dados que só carregam informação.

```csharp
// Application/Models/NovaTransacao.cs
public record NovaTransacao(
    string Id, decimal Valor, string NumeroContaRemetente,
    string NumeroContaDestinatario, string Tipo, string Moeda, DateTime Timestamp);

// Cópia mudando um campo só (usado nos testes)
var outra = nova with { NumeroContaRemetente = "conta_inexistente" };
```

**`class` × `record`:** `class` compara por *referência* (é o mesmo objeto na
memória?). `record` compara por *valor* (os campos são iguais?). Use `record`
para dados (`NovaTransacao`, `ResultadoRecebimento`) e `class` para objetos
com identidade (`Transacao`).

## 5. Pattern matching

```csharp
if (transacao is null) return;

// Domain/Policies/PoliticaDeRisco.cs — lê como frase
public static bool PodeSerReavaliada(StatusTransacao status) =>
    status is StatusTransacao.Suspeita or StatusTransacao.AceitaProvisoria;
```

### Switch expression

Transforma cada caso num valor, sem `if` encadeado:

```csharp
// Api/Controllers/TransactionsController.cs
return resultado.Desfecho switch
{
    DesfechoRecebimento.ContaNaoEncontrada => BadRequest(...),
    DesfechoRecebimento.Duplicada          => Conflict(...),
    DesfechoRecebimento.Bloqueada          => StatusCode(403, ...),
    _                                      => Accepted(...)   // '_' = qualquer outro
};
```

## 6. LINQ

Consultas declarativas sobre coleções:

```csharp
var resultado = await _context.Transacoes
    .Where(t => t.Status == StatusTransacao.Suspeita)
    .OrderByDescending(t => t.CreatedAt)
    .Skip((pagina - 1) * tamanho)   // paginação: pula as anteriores
    .Take(tamanho)                   // pega N
    .ToListAsync();                  // ← só aqui a query vai ao banco
```

Com EF Core o LINQ é traduzido para SQL. Tudo antes do `ToListAsync()` é só
**montagem** da query (*deferred execution*).

## 7. Generics

Um código que serve para vários tipos:

```csharp
Task<float> CalcularRiscoAsync(...);          // operação async que devolve float
List<TransacaoFeatures> dataset = new();
ConcurrentDictionary<string, SlidingWindow> _janelas = new();   // dicionário thread-safe
ValueConverter<string, string>                 // converte texto ↔ texto cifrado
```

## 8. Tuplas

Devolver mais de um valor sem criar classe:

```csharp
// Domain/Policies/PoliticaDeRisco.cs
public static (StatusTransacao Status, string? Motivo) Decidir(float score) =>
    score >= LimiarBloqueio
        ? (StatusTransacao.Bloqueada, MotivoBloqueioModelo)
        : (StatusTransacao.Aprovada, null);

// Desconstrução no uso (ReavaliacaoService)
var (novoStatus, motivo) = PoliticaDeRisco.Decidir(score);
```

> **Tupla × Result.** O `TransacaoService` já devolveu uma tupla
> `(Transacao, bool IsDuplicata)`. Virou o record `ResultadoRecebimento`
> porque passaram a existir **quatro** desfechos (aceita, bloqueada, duplicada,
> conta inexistente) — um `bool` não dava conta. Tupla é ótima para dois
> valores simples; quando o resultado tem "tipos" de desfecho, use um record.
> Veja [05 — Design patterns](05-design-patterns.md#result).

## 9. Extension methods

Métodos estáticos que parecem pertencer a outro tipo, por causa do `this`:

```csharp
// Api/Mappers/TransacaoMapper.cs
public static string ParaTexto(this StatusTransacao status) =>
    status.ToString().ToLowerInvariant();

status.ParaTexto();                     // parece método do enum

// Application/DependencyInjection.cs
services.AddVeroApplication();           // mesma ideia

// Domain/Rules/RegrasExtensions.cs
var violada = await regras.PrimeiraVioladaAsync(transacao);
```

## 10. Outros recursos que aparecem

```csharp
// File-scoped namespace — um nível de indentação a menos
namespace Vero.Domain.Entities;

// Top-level statements — Program.cs sem class Program / static void Main
var builder = WebApplication.CreateBuilder(args);

// const (tempo de compilação) × static readonly (tempo de execução)
public const decimal LimiteValor = 100_000m;
public static readonly TimeSpan JanelaTempo = TimeSpan.FromMinutes(5);

// Interpolação de string
$"Dashboard conectado. ConnectionId: {Context.ConnectionId}"

// Collection expression (C# 12)
public List<TransacaoResumoDto> Transacoes { get; set; } = [];

// Primary constructor (C# 12) — Worker/Worker.cs
public class AsyncWorker(ILogger<AsyncWorker> logger, IServiceScopeFactory scopeFactory,
    IFilaTransacao filaTransacao) : BackgroundService { ... }

// Interface vazia herdando outra — Domain/Rules/IRegraSincrona.cs
public interface IRegraSincrona : IRule;
```

---

**Anterior:** [← 01 — Visão geral](01-visao-geral.md) · **Próximo:** [03 — Clean Architecture →](03-clean-architecture.md)
