using Vero.Domain.Entities;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules.Async;

namespace Vero.Application.Services;

/// <summary>
/// Junta as duas etapas para obter o score de uma transação: contar quantas
/// transações o remetente fez na janela de velocity e perguntar ao modelo.
///
/// Antes esse trecho estava copiado no TransactionsController e no
/// DashboardController, cada um com seu "AddMinutes(-5)" — se alguém mudasse
/// a janela da VelocityRule, os controllers ficariam desatualizados sem
/// ninguém perceber. Agora a janela vem de um lugar só.
///
/// Repare que esta classe não tem interface: ela é lógica de aplicação pura,
/// testável através das dependências que recebe. Interface faz sentido quando
/// existe motivo real para trocar a implementação (ex: banco, fila, SNS).
/// </summary>
public class CalculadoraDeRisco
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IRiskScoringService _riskScoringService;

    public CalculadoraDeRisco(
        ITransacaoRepository transacaoRepository,
        IRiskScoringService riskScoringService)
    {
        _transacaoRepository = transacaoRepository;
        _riskScoringService = riskScoringService;
    }

    /// <param name="referencia">Fim da janela de velocity.</param>
    public async Task<float> CalcularAsync(Transacao transacao, Conta? remetente, DateTime referencia)
    {
        var transacoesRecentes = await _transacaoRepository.ContarPorRemetenteNoPeriodoAsync(
            transacao.RemetenteId,
            referencia - VelocityRule.JanelaTempo,
            referencia);

        return await _riskScoringService.CalcularRiscoAsync(transacao, remetente, transacoesRecentes);
    }
}
