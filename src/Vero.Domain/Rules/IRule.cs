using Vero.Domain.Entities;

namespace Vero.Domain.Rules;

/// <summary>
/// Contrato comum a toda regra de detecção de anomalia (Strategy Pattern).
///
/// Cada regra é uma "estratégia" intercambiável: quem usa as regras só
/// conhece esta interface, nunca a classe concreta. Para criar uma regra
/// nova basta uma classe nova implementando <see cref="IRegraSincrona"/> ou
/// <see cref="IRegraAssincrona"/> e uma linha de registro no DI — nenhum
/// serviço existente precisa mudar (Open/Closed Principle).
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
