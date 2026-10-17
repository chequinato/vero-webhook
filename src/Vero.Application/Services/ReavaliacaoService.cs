using Vero.Application.Interfaces;
using Vero.Application.Models;
using Vero.Domain.Interfaces;
using Vero.Domain.Policies;

namespace Vero.Application.Services;

/// <summary>
/// Roda o modelo de novo sobre uma transação parada em revisão e resolve o
/// caso. Não existe decisão manual: o operador pede a avaliação, quem decide
/// é o modelo (via <see cref="PoliticaDeRisco"/>).
/// </summary>
public class ReavaliacaoService : IReavaliacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IContaRepository _contaRepository;
    private readonly CalculadoraDeRisco _calculadoraDeRisco;
    private readonly INotificadorTransacao _notificador;

    public ReavaliacaoService(
        ITransacaoRepository transacaoRepository,
        IContaRepository contaRepository,
        CalculadoraDeRisco calculadoraDeRisco,
        INotificadorTransacao notificador)
    {
        _transacaoRepository = transacaoRepository;
        _contaRepository = contaRepository;
        _calculadoraDeRisco = calculadoraDeRisco;
        _notificador = notificador;
    }

    public async Task<ResultadoReavaliacao> ReavaliarAsync(string transacaoId)
    {
        var transacao = await _transacaoRepository.ObterPorIdAsync(transacaoId);
        if (transacao is null)
            return ResultadoReavaliacao.NaoEncontrada();

        if (!PoliticaDeRisco.PodeSerReavaliada(transacao.Status))
            return ResultadoReavaliacao.JaDecidida(transacao);

        var remetente = await _contaRepository.ObterPorIdAsync(transacao.RemetenteId);

        // Janela de velocity ancorada no timestamp da própria transação, para
        // o modelo ver as mesmas features de quando ela chegou.
        var score = await _calculadoraDeRisco.CalcularAsync(transacao, remetente, transacao.Timestamp);
        var (novoStatus, motivo) = PoliticaDeRisco.Decidir(score);

        // Status e score gravados juntos numa transação de banco só
        await _transacaoRepository.AtualizarStatusEScoreAsync(transacaoId, novoStatus, motivo, score);

        transacao.Status = novoStatus;
        transacao.Motivo = motivo;
        transacao.RiskScore = score;

        await _notificador.StatusAtualizadoAsync(transacao);

        return ResultadoReavaliacao.Reavaliada(transacao);
    }
}
