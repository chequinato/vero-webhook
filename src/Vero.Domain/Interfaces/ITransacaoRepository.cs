using Vero.Domain.Entities;
using Vero.Domain.Enums;

namespace Vero.Domain.Interfaces;

public interface ITransacaoRepository
{
    Task<Transacao?> ObterPorIdAsync(string id);
    Task<bool> ExisteAsync(string id);
    Task AdicionarAsync(Transacao transacao);
    Task AtualizarStatusAsync(string transacaoId, StatusTransacao novoStatus, string? motivo);
    Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync();

    /// <summary>
    /// Conta quantas transações o remetente fez num período (para regra de velocity).
    /// </summary>
    Task<int> ContarPorRemetenteNoPeriodoAsync(int remetenteId, DateTime inicio, DateTime fim);
}
