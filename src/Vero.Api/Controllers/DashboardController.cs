using Microsoft.AspNetCore.Mvc;
using Vero.Domain.Interfaces;

namespace Vero.Api.Controllers;

/// <summary>
/// Endpoints do dashboard — estatísticas, listagem paginada e volume por hora.
/// Não passa pelo middleware HMAC (GET only, sem mutação).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly ITransacaoRepository _transacaoRepository;

    public DashboardController(ITransacaoRepository transacaoRepository)
    {
        _transacaoRepository = transacaoRepository;
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
}
