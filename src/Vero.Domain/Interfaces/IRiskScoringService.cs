using Vero.Domain.Entities;

namespace Vero.Domain.Interfaces;

/// <summary>
/// Serviço de scoring de risco baseado em Machine Learning.
/// Recebe uma transação e retorna uma probabilidade de fraude (0.0 a 1.0).
/// </summary>
public interface IRiskScoringService
{
    /// <summary>
    /// Calcula o risco de fraude de uma transação.
    /// </summary>
    /// <param name="transacao">Transação a avaliar</param>
    /// <param name="remetente">Conta remetente (pode ser null)</param>
    /// <param name="transacoesRecentes">Número de transações do remetente nos últimos 5 minutos (velocity)</param>
    /// <returns>Probabilidade de fraude entre 0.0 (seguro) e 1.0 (fraude certa)</returns>
    Task<float> CalcularRiscoAsync(Transacao transacao, Conta? remetente, int transacoesRecentes = 0);
}
