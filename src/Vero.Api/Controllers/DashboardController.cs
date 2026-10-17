using Microsoft.AspNetCore.Mvc;
using Vero.Api.Mappers;
using Vero.Application.Interfaces;
using Vero.Application.Models;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Policies;

namespace Vero.Api.Controllers;

/// <summary>
/// Endpoints do dashboard — estatísticas, listagem paginada, volume por hora
/// e reavaliação de transações pelo modelo.
///
/// As rotas de leitura falam direto com o repositório: são consultas sem
/// regra de negócio, e criar um service só para repassar a chamada seria
/// cerimônia sem ganho (YAGNI). Já a reavaliação **muda estado e tem regra**,
/// então passa pela Application (<see cref="IReavaliacaoService"/>).
///
/// Nenhuma rota daqui passa pelo middleware HMAC: ele só cobre POST
/// /transactions, o webhook de entrada. Isso é adequado enquanto o painel
/// roda em rede interna, mas a reavaliação muta estado — antes de expor
/// este host publicamente, a rota precisa de autenticação de operador.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IReavaliacaoService _reavaliacaoService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ITransacaoRepository transacaoRepository,
        IReavaliacaoService reavaliacaoService,
        ILogger<DashboardController> logger)
    {
        _transacaoRepository = transacaoRepository;
        _reavaliacaoService = reavaliacaoService;
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

        StatusTransacao? filtro = null;
        if (!string.IsNullOrEmpty(status) &&
            Enum.TryParse<StatusTransacao>(status, ignoreCase: true, out var parsed))
        {
            filtro = parsed;
        }

        var (items, total) = await _transacaoRepository.ListarPaginadoAsync(page, size, filtro);

        return Ok(new
        {
            items = items.Select(t => new
            {
                t.Id,
                status = t.Status.ParaTexto(),
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
        var modelPath = Vero.ML.DependencyInjection.CaminhoModeloPadrao;
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
    /// o caso: no corte ou acima vira bloqueada, abaixo vira aprovada.
    /// Transação já decidida devolve 409; id inexistente devolve 404.
    /// </summary>
    [HttpPost("transactions/{id}/reavaliar")]
    public async Task<IActionResult> Reavaliar(string id)
    {
        var resultado = await _reavaliacaoService.ReavaliarAsync(id);
        var transacao = resultado.Transacao;

        if (resultado.Desfecho == DesfechoReavaliacao.NaoEncontrada)
            return NotFound(new { erro = "Transação não encontrada." });

        if (resultado.Desfecho == DesfechoReavaliacao.JaDecidida)
        {
            return Conflict(new
            {
                erro = "Só transações em revisão podem ser reavaliadas.",
                status = transacao!.Status.ParaTexto()
            });
        }

        var statusTexto = transacao!.Status.ParaTexto();

        _logger.LogInformation(
            "Reavaliação de {Id}: score {Score:F3} -> {Status}", id, transacao.RiskScore, statusTexto);

        return Ok(new
        {
            id,
            status = statusTexto,
            motivo = transacao.Motivo,
            riskScore = transacao.RiskScore,
            limiar = PoliticaDeRisco.LimiarBloqueio
        });
    }
}
