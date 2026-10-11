using Microsoft.AspNetCore.Mvc;
using Vero.Api.DTOs.Requests;
using Vero.Api.DTOs.Responses;
using Vero.Api.Mappers;
using Vero.Application.Interfaces;
using Vero.Application.Models;

namespace Vero.Api.Controllers;

/// <summary>
/// Webhook de transações. O controller só faz três coisas:
/// valida a entrada, chama a Application e traduz o resultado para HTTP.
/// Regra de negócio aqui dentro é sinal de que algo está no lugar errado.
/// </summary>
[ApiController]
[Route("[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransacaoService _transacaoService;

    public TransactionsController(ITransacaoService transacaoService)
    {
        _transacaoService = transacaoService;
    }

    /// <summary>
    /// POST /transactions — recebe uma transação via webhook.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceberTransacao([FromBody] TransacaoRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var resultado = await _transacaoService.ProcessarTransacaoAsync(request.ParaNovaTransacao());
        var transacao = resultado.Transacao;

        return resultado.Desfecho switch
        {
            DesfechoRecebimento.ContaNaoEncontrada =>
                BadRequest(new { erro = "Conta remetente ou destinatária não encontrada." }),

            DesfechoRecebimento.Duplicada => Conflict(new TransacaoResponseDto
            {
                Status = transacao!.Status.ParaTexto(),
                Motivo = "transacao_duplicada",
                Id = transacao.Id
            }),

            DesfechoRecebimento.Bloqueada => StatusCode(403, new TransacaoResponseDto
            {
                Status = "bloqueada",
                Motivo = transacao!.Motivo,
                RiskScore = transacao.RiskScore
            }),

            _ => Accepted(new TransacaoResponseDto
            {
                Status = "aceita_provisoria",
                Id = transacao!.Id,
                RiskScore = transacao.RiskScore
            })
        };
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
            Status = transacao.Status.ParaTexto(),
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

        return Ok(new TransacoesSuspeitasResponseDto
        {
            Total = suspeitas.Count,
            Transacoes = suspeitas.Select(t => new TransacaoResumoDto
            {
                Id = t.Id,
                Status = t.Status.ParaTexto(),
                Motivo = t.Motivo
            }).ToList()
        });
    }
}
