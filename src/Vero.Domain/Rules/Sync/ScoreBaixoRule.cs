using Vero.Domain.Entities;
using Vero.Domain.Interfaces;

namespace Vero.Domain.Rules.Sync;

/// <summary>
/// Bloqueia transações quando a conta remetente tem score de confiança abaixo de 35 (regra síncrona).
/// </summary>
public class ScoreBaixoRule : IRule
{
    public const int ScoreMinimo = 35;

    private readonly IContaRepository _contaRepository;

    public ScoreBaixoRule(IContaRepository contaRepository)
    {
        _contaRepository = contaRepository;
    }

    public string Nome => "score_baixo";

    public async Task<bool> Avaliar(Transacao transacao)
    {
        var conta = await _contaRepository.ObterPorIdAsync(transacao.RemetenteId);
        if (conta is null) return false;

        return conta.Score < ScoreMinimo;
    }
}
