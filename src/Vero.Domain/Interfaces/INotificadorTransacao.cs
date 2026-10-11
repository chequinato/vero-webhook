using Vero.Domain.Entities;

namespace Vero.Domain.Interfaces;

/// <summary>
/// Avisa quem estiver acompanhando (o dashboard) que algo aconteceu com uma
/// transação — Observer Pattern.
///
/// A implementação real usa SignalR e mora na Vero.Api. Os services da
/// Application só conhecem esta interface, então não dependem de SignalR
/// (Dependency Inversion Principle): dá pra trocar por e-mail, Slack ou um
/// mock nos testes sem tocar em uma linha de regra de negócio.
/// </summary>
public interface INotificadorTransacao
{
    Task TransacaoRecebidaAsync(Transacao transacao);
    Task StatusAtualizadoAsync(Transacao transacao);
}
