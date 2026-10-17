# 06 — As camadas na prática

> **🧠 Pra gravar**
> - **Domain**: entidades, regras e políticas — zero dependências.
> - **Application**: a ordem das coisas — `TransacaoService`, `ReavaliacaoService`, `AnomalyDetectionService`.
> - **Infrastructure**: como persistir, enfileirar e alertar.
> - **Api**: traduz HTTP ↔ Application. **Regra de negócio no controller é sinal de algo no lugar errado.**

## Domain

### Entidades

Objetos do negócio com **identidade** (`Transacao` tem `Id`):

```csharp
public class Transacao
{
    public string Id { get; set; } = string.Empty;   // vem do webhook, não é gerado
    public int RemetenteId { get; set; }             // FK para Conta
    public int DestinatarioId { get; set; }
    public decimal Valor { get; set; }
    public string Tipo { get; set; } = string.Empty; // "pix"
    public string Moeda { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }          // quando aconteceu
    public StatusTransacao Status { get; set; }
    public string? Motivo { get; set; }              // só se bloqueada/suspeita
    public float? RiskScore { get; set; }            // 0.0 a 1.0
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Conta? Remetente { get; set; }            // navigation property (EF Core)
    public Conta? Destinatario { get; set; }
}
```

`Conta` tem `NumeroConta` e `Titular` (criptografados no banco) e um `Score`
de confiança de 0 a 100.

### Enum de status

```csharp
public enum StatusTransacao { AceitaProvisoria, Bloqueada, Aprovada, Suspeita }
```

No banco é gravado como **texto** (`"Bloqueada"`), não como número — mais
legível e não quebra se alguém reordenar o enum.

### Regras

| Regra | Tipo | Nome (vira o motivo) | Viola quando |
|---|---|---|---|
| `ValorAltoRule` | síncrona | `valor_alto` | valor > R$100.000 |
| `ScoreBaixoRule` | síncrona | `score_baixo` | score do remetente < 35 |
| `VelocityRule` | assíncrona | `velocity` | ≥ 3 transações do remetente em 5 min |
| `HorarioEstranhoRule` | assíncrona | `horario_estranho` | entre 1h e 5h |
| `ValorRedondoRule` | assíncrona | `valor_redondo` | múltiplo de R$1.000, a partir de R$1.000 |

```csharp
// Regra simples, sem dependência
public class ValorAltoRule : IRegraSincrona
{
    public const decimal LimiteValor = 100_000m;
    public string Nome => "valor_alto";

    public Task<bool> Avaliar(Transacao transacao) =>
        Task.FromResult(transacao.Valor > LimiteValor);
}

// Regra que consulta o banco — recebe o repositório por DI
public class ScoreBaixoRule : IRegraSincrona
{
    public const int ScoreMinimo = 35;
    private readonly IContaRepository _contaRepository;
    public string Nome => "score_baixo";

    public async Task<bool> Avaliar(Transacao transacao)
    {
        var conta = await _contaRepository.ObterPorIdAsync(transacao.RemetenteId);
        if (conta is null) return false;   // sem conta, esta regra não opina
        return conta.Score < ScoreMinimo;
    }
}
```

A `VelocityRule` conta a janela **a partir do timestamp da própria transação**
(`transacao.Timestamp - 5min` até `transacao.Timestamp`), não a partir de
"agora". Assim o resultado não depende de quando o Worker rodou.

### Política

`PoliticaDeRisco` concentra o corte de 70% do modelo e quais status podem ser
reavaliados (veja [05 — Design patterns](05-design-patterns.md#policy)).

## Application

### `TransacaoService` — o orquestrador do recebimento

```csharp
public async Task<ResultadoRecebimento> ProcessarTransacaoAsync(NovaTransacao nova)
{
    var remetente = await _contaRepository.ObterPorNumeroContaAsync(nova.NumeroContaRemetente);
    var destinatario = await _contaRepository.ObterPorNumeroContaAsync(nova.NumeroContaDestinatario);
    if (remetente is null || destinatario is null)
        return ResultadoRecebimento.ContaNaoEncontrada();

    // Proteção contra replay
    var existente = await _transacaoRepository.ObterPorIdAsync(nova.Id);
    if (existente is not null)
        return ResultadoRecebimento.Duplicada(existente);

    var transacao = new Transacao { /* ... dados de 'nova' + ids das contas ... */ };
    transacao.RiskScore = await _calculadoraDeRisco.CalcularAsync(transacao, remetente, DateTime.UtcNow);

    var resultado = await AplicarRegrasSincronasAsync(transacao);   // bloqueia ou enfileira
    await _notificador.TransacaoRecebidaAsync(transacao);
    return resultado;
}
```

Repare na **ordem**: conta → duplicata → risco → regras. Checar duplicata
**antes** de calcular evita gastar o modelo com um reenvio.

### `CalculadoraDeRisco` — sem copiar a janela de velocity

```csharp
public async Task<float> CalcularAsync(Transacao transacao, Conta? remetente, DateTime referencia)
{
    var recentes = await _transacaoRepository.ContarPorRemetenteNoPeriodoAsync(
        transacao.RemetenteId, referencia - VelocityRule.JanelaTempo, referencia);

    return await _riskScoringService.CalcularRiscoAsync(transacao, remetente, recentes);
}
```

Antes, os dois controllers faziam esse cálculo, cada um com seu
`AddMinutes(-5)`. Mudar a janela na `VelocityRule` deixaria os dois
desatualizados sem ninguém perceber. Agora a janela vem **de um lugar só**.

### `AnomalyDetectionService` — a análise em background

```csharp
public async Task AnalisarAsync(string transacaoId)
{
    var transacao = await _transacaoRepository.ObterPorIdAsync(transacaoId);
    if (transacao is null) return;
    if (transacao.Status != StatusTransacao.AceitaProvisoria) return;   // guarda de estado

    var regraViolada = await _regrasAssincronas.PrimeiraVioladaAsync(transacao);
    if (regraViolada is not null)
    {
        await _transacaoRepository.AtualizarStatusAsync(transacaoId, StatusTransacao.Suspeita, regraViolada.Nome);
        await _alertaService.EnviarAlertaAsync(transacaoId, regraViolada.Nome);
        return;
    }

    await _transacaoRepository.AtualizarStatusAsync(transacaoId, StatusTransacao.Aprovada, motivo: null);
}
```

A **guarda de estado** importa: se a mesma mensagem chegar duas vezes pela
fila, a segunda não faz nada (a transação já saiu de `AceitaProvisoria`).

### `ReavaliacaoService`

Carrega a transação, confere com `PoliticaDeRisco.PodeSerReavaliada`, recalcula
o risco **na janela original** (`transacao.Timestamp`), decide com
`PoliticaDeRisco.Decidir`, grava status + score juntos e notifica o dashboard.
Reavaliar com features de hoje sobre uma transação de ontem daria um número
sem significado.

## Infrastructure

### Repositórios

```csharp
public class TransacaoRepository : ITransacaoRepository
{
    private readonly VeroDbContext _context;

    public async Task<Transacao?> ObterPorIdAsync(string id) =>
        await _context.Transacoes.FindAsync(id);

    // Estatísticas numa ida só ao banco: agrupa tudo num grupo e conta por status
    public async Task<TransacaoStats> ObterEstatisticasAsync()
    {
        var contadores = await _context.Transacoes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Aprovadas = g.Count(t => t.Status == StatusTransacao.Aprovada),
                // ...
            })
            .FirstAsync();
        // ...
    }
}
```

### Escrita atômica

A reavaliação produz status novo **e** score novo. Gravar em duas chamadas
deixaria uma janela em que a linha mostra a decisão nova com o score velho.
Por isso existe um método que faz tudo num `SaveChangesAsync` só, e ainda
registra a mudança no histórico:

```csharp
public async Task AtualizarStatusEScoreAsync(
    string transacaoId, StatusTransacao novoStatus, string? motivo, float riskScore)
{
    var transacao = await _context.Transacoes.FindAsync(transacaoId);
    if (transacao is null) return;

    var statusAnterior = transacao.Status.ToString();
    transacao.Status = novoStatus;
    transacao.Motivo = motivo;
    transacao.RiskScore = riskScore;

    await _context.TransacaoHistoricoStatus.AddAsync(new TransacaoHistoricoStatus { /* de → para */ });
    await _context.SaveChangesAsync();   // uma transação de banco: tudo ou nada
}
```

O EF Core percebe sozinho quais colunas mudaram na entidade carregada pelo
`FindAsync` — é o **change tracking**. Não é preciso escrever o `UPDATE`.

### Fila e alertas

| Interface | Dev (padrão) | Produção |
|---|---|---|
| `IFilaTransacao` | `InMemoryFilaTransacao` (`ConcurrentQueue`) | `SqsFilaTransacao` (AWS SQS) |
| `IAlertaService` | `ConsoleAlertaService` (log) | `SnsAlertaService` (AWS SNS) |

```csharp
public Task<string?> ConsumirProximaAsync() =>
    Task.FromResult(_fila.TryDequeue(out var id) ? id : null);   // 'out' cria a variável inline
```

> Repare: a Api e o Worker rodam em **processos separados**, cada um com sua
> fila em memória. Localmente, o que a Api enfileira **não chega** no Worker.
> Com o SQS os dois falam com a mesma fila de verdade.

## Api

### Controllers finos

```csharp
[ApiController]
[Route("[controller]")]                       // → /transactions
public class TransactionsController : ControllerBase
{
    private readonly ITransacaoService _transacaoService;   // uma dependência só

    [HttpPost]
    public async Task<IActionResult> ReceberTransacao([FromBody] TransacaoRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var resultado = await _transacaoService.ProcessarTransacaoAsync(request.ParaNovaTransacao());
        return resultado.Desfecho switch { /* desfecho → status HTTP */ };
    }
}
```

| Rota | O que faz |
|---|---|
| `POST /transactions` | Recebe o webhook |
| `GET /transactions/{id}` | Consulta status |
| `GET /transactions?suspicious=true` | Lista suspeitas e bloqueadas |
| `GET /health` | Healthcheck |
| `GET /api/dashboard/stats`, `/transactions`, `/timeline`, `/ml/metrics` | Leituras do painel |
| `POST /api/dashboard/transactions/{id}/reavaliar` | Reavaliação pelo modelo |

| Método | Status | Quando |
|---|---|---|
| `Ok(obj)` | 200 | Sucesso com dados |
| `Accepted(obj)` | 202 | Aceito para processamento |
| `BadRequest(obj)` | 400 | Dados inválidos / conta inexistente |
| `NotFound(obj)` | 404 | Não encontrado |
| `Conflict(obj)` | 409 | Duplicata, ou reavaliar algo já decidido |
| `StatusCode(403, obj)` | 403 | Bloqueada |

### DTOs

Separam o formato do JSON das entidades internas:

```csharp
public class TransacaoRequestDto
{
    [Required] public string Id { get; set; } = string.Empty;
    [Required, Range(0.01, double.MaxValue)] public decimal Valor { get; set; }
    [Required] public string Remetente { get; set; } = string.Empty;   // número da conta, não o id
    // ...
}
```

O webhook manda o **número** da conta; a entidade guarda o **id**. O DTO é a
tradução entre o mundo de fora e o de dentro, e o `TransacaoMapper` faz a
conversão.

### SignalR — o servidor empurra, o cliente não pergunta

```csharp
public class TransactionHubNotifier : INotificadorTransacao
{
    private readonly IHubContext<TransactionHub> _hubContext;

    public Task TransacaoRecebidaAsync(Transacao t) =>
        _hubContext.Clients.All.SendAsync("TransacaoRecebida", new { id = t.Id, status = t.Status.ParaTexto(), /* ... */ });
}
```

Sem SignalR, o dashboard perguntaria "tem novidade?" a cada X segundos
(*polling*). Com ele, o evento chega no instante em que acontece.

### CORS

O navegador bloqueia chamadas de `localhost:5173` (dashboard) para
`localhost:5000` (API) por serem origens diferentes. A política
`DashboardPolicy` libera as origens do Vite e usa `AllowCredentials()`, que o
SignalR exige.

> ⚠️ **Dívida reconhecida:** o HMAC só cobre `POST /transactions`. As rotas
> `/api/dashboard/*` não têm autenticação — aceitável em rede interna, mas a
> reavaliação **muda estado**. Antes de expor o host, ela precisa de
> autenticação de operador.

---

**Anterior:** [← 05 — Design patterns](05-design-patterns.md) · **Próximo:** [07 — Banco de dados →](07-banco-de-dados.md)
