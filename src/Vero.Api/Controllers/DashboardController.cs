using Microsoft.AspNetCore.Mvc;
using Vero.Api.Hubs;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;

namespace Vero.Api.Controllers;

/// <summary>
/// Endpoints do dashboard — estatísticas, listagem paginada, volume por hora
/// e reavaliação de transações pelo modelo.
///
/// Nenhuma rota daqui passa pelo middleware HMAC: ele só cobre POST
/// /transactions, o webhook de entrada. Isso é adequado enquanto o painel
/// roda em rede interna, mas a reavaliação **muta estado** — antes de expor
/// este host publicamente, a rota precisa de autenticação de operador.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    /// <summary>
    /// Corte de bloqueio do modelo. É o mesmo 70% que o painel imprime sobre
    /// a distribuição de risco — os dois têm de sair daqui, senão a linha
    /// desenhada na tela deixa de descrever a decisão real.
    /// </summary>
    private const float LimiarBloqueio = 0.70f;

    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IRiskScoringService _riskScoringService;
    private readonly TransactionHubNotifier _hubNotifier;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ITransacaoRepository transacaoRepository,
        IContaRepository contaRepository,
        IRiskScoringService riskScoringService,
        TransactionHubNotifier hubNotifier,
        ILogger<DashboardController> logger)
    {
        _transacaoRepository = transacaoRepository;
        _contaRepository = contaRepository;
        _riskScoringService = riskScoringService;
        _hubNotifier = hubNotifier;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/dashboard/stats — estatísticas agregadas
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> ObterEstatisticas()
    {
        var stats = await _transacaoRepository.ObterEstatisticasAsync();
        return Ok(stats);
    }

    /// <summary>
    /// GET /api/dashboard/transactions?page=1&size=20&status=suspeita
    /// Listagem paginada de transações para o dashboard.
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> ListarTransacoes(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? status = null)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        Domain.Enums.StatusTransacao? filtro = null;
        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<Domain.Enums.StatusTransacao>(status, ignoreCase: true, out var parsed))
        {
            filtro = parsed;
        }

        var (items, total) = await _transacaoRepository.ListarPaginadoAsync(page, size, filtro);

        return Ok(new
        {
            items = items.Select(t => new
            {
                t.Id,
                status = t.Status.ToString().ToLowerInvariant(),
                t.Valor,
                t.Tipo,
                t.Moeda,
                t.Motivo,
                t.RiskScore,
                t.Timestamp,
                t.RemetenteId,
                t.DestinatarioId,
                t.CreatedAt
            }),
            total,
            page,
            size,
            totalPages = (int)Math.Ceiling(total / (double)size)
        });
    }

    /// <summary>
    /// GET /api/dashboard/timeline — volume de transações por hora (últimas 24h)
    /// </summary>
    [HttpGet("timeline")]
    public async Task<IActionResult> ObterTimeline()
    {
        var timeline = await _transacaoRepository.ObterVolumePorHoraAsync();
        return Ok(timeline);
    }

    /// <summary>
    /// GET /api/dashboard/ml/metrics — informações do modelo de ML
    /// </summary>
    [HttpGet("ml/metrics")]
    public IActionResult ObterMetricasMl()
    {
        var modelPath = Path.Combine(AppContext.BaseDirectory, "Models", "fraud_model.zip");
        var modelExists = System.IO.File.Exists(modelPath);

        return Ok(new
        {
            modelLoaded = modelExists,
            modelPath = modelExists ? modelPath : null,
            lastTrained = modelExists ? (DateTime?)System.IO.File.GetLastWriteTimeUtc(modelPath) : null,
            algorithm = "FastTree (Gradient Boosted Decision Trees)",
            features = new[]
            {
                "Valor", "HoraDoDia", "ScoreRemetente", "TransacoesRecentes",
                "IsValorRedondo", "IsHorarioEstranho", "RazaoValorScore"
            }
        });
    }

    /// <summary>
    /// POST /api/dashboard/transactions/{id}/reavaliar
    ///
    /// Roda o modelo de novo sobre uma transação parada em revisão e resolve
    /// o caso: acima do corte vira bloqueada, abaixo vira aprovada. É o botão
    /// que o analista aperta na fila — não há decisão manual, quem decide é o
    /// modelo; o operador só pede a decisão.
    ///
    /// Só aceita transações ainda pendentes (suspeita ou aceita provisória).
    /// Reavaliar algo já decidido devolve 409, para o painel não conseguir
    /// desfazer um bloqueio sem que isso passe por um fluxo próprio.
    /// </summary>
    [HttpPost("transactions/{id}/reavaliar")]
    public async Task<IActionResult> Reavaliar(string id)
    {
        var transacao = await _transacaoRepository.ObterPorIdAsync(id);
        if (transacao is null)
            return NotFound(new { erro = "Transação não encontrada." });

        if (transacao.Status is not (StatusTransacao.Suspeita or StatusTransacao.AceitaProvisoria))
        {
            return Conflict(new
            {
                erro = "Só transações em revisão podem ser reavaliadas.",
                status = transacao.Status.ToString().ToLowerInvariant()
            });
        }

        var remetente = await _contaRepository.ObterPorIdAsync(transacao.RemetenteId);

        // Mesma janela de velocity usada na entrada, para o modelo ver a
        // transação sob as mesmas features com que foi treinado.
        var transacoesRecentes = await _transacaoRepository.ContarPorRemetenteNoPeriodoAsync(
            transacao.RemetenteId,
            transacao.Timestamp.AddMinutes(-5),
            transacao.Timestamp);

        var score = await _riskScoringService.CalcularRiscoAsync(transacao, remetente, transacoesRecentes);

        var bloqueia = score >= LimiarBloqueio;
        var novoStatus = bloqueia ? StatusTransacao.Bloqueada : StatusTransacao.Aprovada;
        var motivo = bloqueia ? "modelo: risco acima do corte" : null;

        await _transacaoRepository.AtualizarStatusEScoreAsync(id, novoStatus, motivo, score);

        var statusTexto = novoStatus.ToString().ToLowerInvariant();

        _logger.LogInformation(
            "Reavaliação de {Id}: score {Score:F3} -> {Status}", id, score, statusTexto);

        await _hubNotifier.NotificarStatusAtualizado(id, statusTexto, motivo, score);

        return Ok(new
        {
            id,
            status = statusTexto,
            motivo,
            riskScore = score,
            limiar = LimiarBloqueio
        });
    }
}
