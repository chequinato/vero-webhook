using Microsoft.AspNetCore.Mvc;
using Vero.Api.DTOs.Requests;
using Vero.Api.DTOs.Responses;
using Vero.Api.Hubs;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;

namespace Vero.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransacaoService _transacaoService;
    private readonly IContaRepository _contaRepository;
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IRiskScoringService _riskScoringService;
    private readonly TransactionHubNotifier _hubNotifier;

    public TransactionsController(
        ITransacaoService transacaoService,
        IContaRepository contaRepository,
        ITransacaoRepository transacaoRepository,
        IRiskScoringService riskScoringService,
        TransactionHubNotifier hubNotifier)
    {
        _transacaoService = transacaoService;
        _contaRepository = contaRepository;
        _transacaoRepository = transacaoRepository;
        _riskScoringService = riskScoringService;
        _hubNotifier = hubNotifier;
    }

    /// <summary>
    /// POST /transactions — recebe uma transação via webhook.
    /// Aplica regras síncronas + ML risk scoring.
    /// Checagem de replay unificada: apenas o TransacaoService verifica duplicatas.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceberTransacao([FromBody] TransacaoRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Resolver contas pelo NumeroConta
        var remetente = await _contaRepository.ObterPorNumeroContaAsync(request.Remetente);
        var destinatario = await _contaRepository.ObterPorNumeroContaAsync(request.Destinatario);

        if (remetente is null || destinatario is null)
            return BadRequest(new { erro = "Conta remetente ou destinatária não encontrada." });

        // Velocity: contar transações do remetente nos últimos 5 minutos
        var transacoesRecentes = await _transacaoRepository.ContarPorRemetenteNoPeriodoAsync(
            remetente.Id,
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow);

        var transacao = new Transacao
        {
            Id = request.Id,
            RemetenteId = remetente.Id,
            DestinatarioId = destinatario.Id,
            Valor = request.Valor,
            Tipo = request.Tipo,
            Moeda = request.Moeda,
            Timestamp = request.Timestamp
        };

        // ML Risk Scoring — calcular antes de processar
        var riskScore = await _riskScoringService.CalcularRiscoAsync(transacao, remetente, transacoesRecentes);
        transacao.RiskScore = riskScore;

        // Processar (o TransacaoService cuida da checagem de duplicata)
        var (resultado, isDuplicata) = await _transacaoService.ProcessarTransacaoAsync(transacao);

        if (isDuplicata)
        {
            return Conflict(new TransacaoResponseDto
            {
                Status = resultado.Status.ToString().ToLowerInvariant(),
                Motivo = "transacao_duplicada",
                Id = resultado.Id
            });
        }

        // Notificar dashboard em tempo real
        await _hubNotifier.NotificarTransacaoRecebida(new
        {
            id = resultado.Id,
            status = resultado.Status.ToString().ToLowerInvariant(),
            valor = resultado.Valor,
            tipo = resultado.Tipo,
            moeda = resultado.Moeda,
            motivo = resultado.Motivo,
            riskScore = resultado.RiskScore,
            timestamp = resultado.Timestamp
        });

        if (resultado.Status == StatusTransacao.Bloqueada)
        {
            return StatusCode(403, new TransacaoResponseDto
            {
                Status = "bloqueada",
                Motivo = resultado.Motivo,
                RiskScore = riskScore
            });
        }

        return Accepted(new TransacaoResponseDto
        {
            Status = "aceita_provisoria",
            Id = resultado.Id,
            RiskScore = riskScore
        });
    }

    /// <summary>
    /// GET /transactions/{id} — consulta o status de uma transação.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> ConsultarStatus(string id)
    {
        var transacao = await _transacaoService.ConsultarStatusAsync(id);
        if (transacao is null)
            return NotFound(new { erro = "Transação não encontrada." });

        return Ok(new TransacaoStatusResponseDto
        {
            Id = transacao.Id,
            Status = transacao.Status.ToString().ToLowerInvariant(),
            Motivo = transacao.Motivo,
            AnalisadaEm = transacao.CreatedAt,
            RiskScore = transacao.RiskScore
        });
    }

    /// <summary>
    /// GET /transactions?suspicious=true — lista transações suspeitas/bloqueadas.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListarTransacoes([FromQuery] bool suspicious = false)
    {
        if (!suspicious)
            return BadRequest(new { erro = "Use ?suspicious=true para listar transações suspeitas." });

        var suspeitas = await _transacaoService.ListarSuspeitasAsync();

        var response = new TransacoesSuspeitasResponseDto
        {
            Total = suspeitas.Count,
            Transacoes = suspeitas.Select(t => new TransacaoResumoDto
            {
                Id = t.Id,
                Status = t.Status.ToString().ToLowerInvariant(),
                Motivo = t.Motivo
            }).ToList()
        };

        return Ok(response);
    }
}
