using Microsoft.AspNetCore.SignalR;

namespace Vero.Api.Hubs;

/// <summary>
/// Hub SignalR para atualizações em tempo real de transações.
/// O dashboard React se conecta aqui e recebe eventos push.
///
/// Eventos emitidos:
///   - TransacaoRecebida: nova transação chegou
///   - StatusAtualizado: status de uma transação mudou (aprovada/suspeita)
///   - AlertaDisparado: anomalia detectada
/// </summary>
public class TransactionHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Conectado",
            $"Dashboard conectado. ConnectionId: {Context.ConnectionId}");

        await base.OnConnectedAsync();
    }
}
