using Microsoft.AspNetCore.SignalR;

namespace Vero.Api.Hubs;

/// <summary>
/// Serviço que envia notificações para o hub SignalR.
/// Injetado nos controllers/services para notificar o dashboard em tempo real.
/// </summary>
public class TransactionHubNotifier
{
    private readonly IHubContext<TransactionHub> _hubContext;

    public TransactionHubNotifier(IHubContext<TransactionHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotificarTransacaoRecebida(object transacaoDto)
    {
        await _hubContext.Clients.All.SendAsync("TransacaoRecebida", transacaoDto);
    }

    public async Task NotificarStatusAtualizado(string transacaoId, string novoStatus, string? motivo, float? riskScore)
    {
        await _hubContext.Clients.All.SendAsync("StatusAtualizado", new
        {
            id = transacaoId,
            status = novoStatus,
            motivo,
            riskScore,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task NotificarAlerta(string transacaoId, string motivo)
    {
        await _hubContext.Clients.All.SendAsync("AlertaDisparado", new
        {
            id = transacaoId,
            motivo,
            timestamp = DateTime.UtcNow
        });
    }
}
