using Vero.Domain.Entities;

namespace Vero.Domain.Rules;

/// <summary>
/// Contrato comum a toda regra de detecção de anomalia (Strategy Pattern).
/// </summary>
public interface IRule
{
    /// <summary>
    /// Nome identificador da regra (ex: "valor_alto", "velocity").
    /// </summary>
    string Nome { get; }

    /// <summary>
    /// Avalia se a transação viola esta regra.
    /// </summary>
    /// <returns>true se a transação é anômala segundo esta regra.</returns>
    Task<bool> Avaliar(Transacao transacao);
}
