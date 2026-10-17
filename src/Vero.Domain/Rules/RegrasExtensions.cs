using Vero.Domain.Entities;

namespace Vero.Domain.Rules;

public static class RegrasExtensions
{
    /// <summary>
    /// Avalia as regras em ordem e devolve a primeira que a transação viola,
    /// ou null se passou em todas. As regras seguintes não são avaliadas.
    ///
    /// Antes este mesmo foreach estava copiado no TransacaoService e no
    /// AnomalyDetectionService (DRY: Don't Repeat Yourself).
    /// </summary>
    public static async Task<IRule?> PrimeiraVioladaAsync(
        this IEnumerable<IRule> regras, Transacao transacao)
    {
        foreach (var regra in regras)
        {
            if (await regra.Avaliar(transacao))
                return regra;
        }

        return null;
    }
}
