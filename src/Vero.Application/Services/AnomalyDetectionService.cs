using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Application.Services;

/// <summary>
/// Roda as regras assíncronas (mais pesadas) sobre uma transação já persistida.
/// Chamado pelo Worker ao consumir uma mensagem da fila.
/// </summary>
public class AnomalyDetectionService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IAlertaService _alertaService;
    private readonly IEnumerable<IRegraAssincrona> _regrasAssincronas;

    public AnomalyDetectionService(
        ITransacaoRepository transacaoRepository,
        IAlertaService alertaService,
        IEnumerable<IRegraAssincrona> regrasAssincronas)
    {
        _transacaoRepository = transacaoRepository;
        _alertaService = alertaService;
        _regrasAssincronas = regrasAssincronas;
    }

    /// <summary>
    /// Analisa uma transação aplicando todas as regras assíncronas.
    /// Atualiza o status para "suspeita" (com alerta) ou "aprovada".
    /// </summary>
    public async Task AnalisarAsync(string transacaoId)
    {
        var transacao = await _transacaoRepository.ObterPorIdAsync(transacaoId);
        if (transacao is null) return;

        // Só analisa transações com status "aceita_provisoria"
        if (transacao.Status != StatusTransacao.AceitaProvisoria) return;

        var regraViolada = await _regrasAssincronas.PrimeiraVioladaAsync(transacao);

        if (regraViolada is not null)
        {
            await _transacaoRepository.AtualizarStatusAsync(
                transacaoId, StatusTransacao.Suspeita, regraViolada.Nome);

            await _alertaService.EnviarAlertaAsync(transacaoId, regraViolada.Nome);
            return;
        }

        // Passou em todas as regras assíncronas: aprovada
        await _transacaoRepository.AtualizarStatusAsync(
            transacaoId, StatusTransacao.Aprovada, motivo: null);
    }
}
