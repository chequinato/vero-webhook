using Vero.Domain.Entities;

namespace Vero.Domain.Interfaces;

/// <summary>
/// Contrato do serviço que orquestra o recebimento e processamento de transações.
/// </summary>
public interface ITransacaoService
{
    /// <summary>
    /// Processa uma transação recebida via webhook: valida, aplica regras síncronas,
    /// persiste e enfileira para análise assíncrona se necessário.
    /// </summary>
    /// <returns>Tupla com a transação processada e um bool indicando se era duplicata.</returns>
    Task<(Transacao Transacao, bool IsDuplicata)> ProcessarTransacaoAsync(Transacao transacao);

    /// <summary>
    /// Consulta o status atual de uma transação.
    /// </summary>
    Task<Transacao?> ConsultarStatusAsync(string transacaoId);

    /// <summary>
    /// Lista todas as transações com status suspeita ou bloqueada.
    /// </summary>
    Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync();
}
