using Vero.Domain.Entities;
using Vero.Domain.Enums;

namespace Vero.Domain.Interfaces;

public interface ITransacaoRepository
{
    Task<Transacao?> ObterPorIdAsync(string id);
    Task<bool> ExisteAsync(string id);
    Task AdicionarAsync(Transacao transacao);
    Task AtualizarStatusAsync(string transacaoId, StatusTransacao novoStatus, string? motivo);

    /// <summary>
    /// Atualiza status e score numa única transação de banco. Usado pela
    /// reavaliação sob demanda, em que o modelo produz um score novo junto
    /// com a decisão — gravar os dois separadamente deixaria uma janela em
    /// que a linha mostra a decisão nova com o score velho.
    /// </summary>
    Task AtualizarStatusEScoreAsync(
        string transacaoId, StatusTransacao novoStatus, string? motivo, float riskScore);
    Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync();

    /// <summary>
    /// Conta quantas transações o remetente fez num período (para regra de velocity).
    /// </summary>
    Task<int> ContarPorRemetenteNoPeriodoAsync(int remetenteId, DateTime inicio, DateTime fim);

    /// <summary>
    /// Lista transações paginadas (para dashboard), ordenadas por data desc.
    /// </summary>
    Task<(IReadOnlyList<Transacao> Items, int Total)> ListarPaginadoAsync(
        int pagina, int tamanhoPagina, StatusTransacao? filtroStatus = null);

    /// <summary>
    /// Retorna estatísticas agregadas das transações.
    /// </summary>
    Task<TransacaoStats> ObterEstatisticasAsync();

    /// <summary>
    /// Retorna volume de transações agrupado por hora (últimas 24h).
    /// </summary>
    Task<IReadOnlyList<VolumeHora>> ObterVolumePorHoraAsync();
}
