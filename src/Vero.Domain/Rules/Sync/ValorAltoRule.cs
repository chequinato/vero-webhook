using Vero.Domain.Entities;

namespace Vero.Domain.Rules.Sync;

/// <summary>
/// Bloqueia transações com valor acima de R$100.000 (regra síncrona).
/// </summary>
public class ValorAltoRule : IRule
{
    public const decimal LimiteValor = 100_000m;

    public string Nome => "valor_alto";

    public Task<bool> Avaliar(Transacao transacao)
    {
        return Task.FromResult(transacao.Valor > LimiteValor);
    }
}
