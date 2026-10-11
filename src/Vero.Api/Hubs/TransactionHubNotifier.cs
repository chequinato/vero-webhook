using Microsoft.AspNetCore.SignalR;
using Vero.Api.Mappers;
using Vero.Domain.Entities;
using Vero.Domain.Interfaces;

namespace Vero.Api.Hubs;

/// <summary>
/// Implementação de <see cref="INotificadorTransacao"/> que empurra eventos
/// para o dashboard via SignalR (Adapter + Observer).
///
/// Mora na Api porque SignalR é detalhe de entrega web; a Application só
/// conhece a interface.
/// </summary>
public class TransactionHubNotifier : INotificadorTransacao
{
    private readonly IHubContext<TransactionHub> _hubContext;

    public TransactionHubNotifier(IHubContext<TransactionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task TransacaoRecebidaAsync(Transacao transacao) =>
        _hubContext.Clients.All.SendAsync("TransacaoRecebida", new
        {
            id = transacao.Id,
            status = transacao.Status.ParaTexto(),
            valor = transacao.Valor,
            tipo = transacao.Tipo,
            moeda = transacao.Moeda,
            motivo = transacao.Motivo,
            riskScore = transacao.RiskScore,
            timestamp = transacao.Timestamp
        });

    public Task StatusAtualizadoAsync(Transacao transacao) =>
        _hubContext.Clients.All.SendAsync("StatusAtualizado", new
        {
            id = transacao.Id,
            status = transacao.Status.ParaTexto(),
            motivo = transacao.Motivo,
            riskScore = transacao.RiskScore,
            timestamp = DateTime.UtcNow
        });

    public Task NotificarAlerta(string transacaoId, string motivo) =>
        _hubContext.Clients.All.SendAsync("AlertaDisparado", new
        {
            id = transacaoId,
            motivo,
            timestamp = DateTime.UtcNow
        });
}
