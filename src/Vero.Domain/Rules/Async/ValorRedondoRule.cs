using Vero.Domain.Entities;

namespace Vero.Domain.Rules.Async;

/// <summary>
/// Marca como suspeita transações com valores redondos (múltiplos de 1000) acima de R$1.000 (regra assíncrona).
/// </summary>
public class ValorRedondoRule : IRule
{
    public const decimal ValorMinimo = 1_000m;

    public string Nome => "valor_redondo";

    public Task<bool> Avaliar(Transacao transacao)
    {
        var redondo = transacao.Valor >= ValorMinimo && transacao.Valor % 1000 == 0;
        return Task.FromResult(redondo);
    }
}
