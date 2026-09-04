using Vero.Domain.Entities;
using Vero.Domain.Interfaces;

namespace Vero.Domain.Rules.Async;

/// <summary>
/// Detecta velocity: múltiplas transações do mesmo remetente em pouco tempo (regra assíncrona).
/// </summary>
public class VelocityRule : IRule
{
    /// <summary>
    /// Janela de tempo para contagem de transações (5 minutos).
    /// </summary>
    public static readonly TimeSpan JanelaTempo = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Número máximo de transações permitidas na janela.
    /// </summary>
    public const int LimiteTransacoes = 3;

    private readonly ITransacaoRepository _transacaoRepository;

    public VelocityRule(ITransacaoRepository transacaoRepository)
    {
        _transacaoRepository = transacaoRepository;
    }

    public string Nome => "velocity";

    public async Task<bool> Avaliar(Transacao transacao)
    {
        var inicio = transacao.Timestamp.Subtract(JanelaTempo);
        var quantidade = await _transacaoRepository.ContarPorRemetenteNoPeriodoAsync(
            transacao.RemetenteId, inicio, transacao.Timestamp);

        return quantidade >= LimiteTransacoes;
    }
}
