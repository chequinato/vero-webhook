using Vero.Domain.Entities;

namespace Vero.Domain.Rules.Async;

/// <summary>
/// Marca como suspeita transações realizadas em horários incomuns — entre 1h e 5h (regra assíncrona).
/// </summary>
public class HorarioEstranhoRule : IRule
{
    public const int HoraInicio = 1;
    public const int HoraFim = 5;

    public string Nome => "horario_estranho";

    public Task<bool> Avaliar(Transacao transacao)
    {
        var hora = transacao.Timestamp.Hour;
        var estranho = hora >= HoraInicio && hora < HoraFim;
        return Task.FromResult(estranho);
    }
}
