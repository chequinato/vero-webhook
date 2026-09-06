using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Application.Services;

/// <summary>
/// Orquestra o recebimento de transações: aplica regras síncronas, persiste,
/// e enfileira para análise assíncrona quando necessário.
/// </summary>
public class TransacaoService : ITransacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IFilaTransacao _filaTransacao;
    private readonly IEnumerable<IRule> _regrasSincronas;

    public TransacaoService(
        ITransacaoRepository transacaoRepository,
        IContaRepository contaRepository,
        IFilaTransacao filaTransacao,
        IEnumerable<IRule> regrasSincronas)
    {
        _transacaoRepository = transacaoRepository;
        _contaRepository = contaRepository;
        _filaTransacao = filaTransacao;
        _regrasSincronas = regrasSincronas;
    }

    public async Task<(Transacao Transacao, bool IsDuplicata)> ProcessarTransacaoAsync(Transacao transacao)
    {
        // Proteção contra replay: se a transação já existe, não processa de novo
        if (await _transacaoRepository.ExisteAsync(transacao.Id))
        {
            var existente = await _transacaoRepository.ObterPorIdAsync(transacao.Id);
            return (existente!, IsDuplicata: true);
        }

        // Aplicar regras síncronas (bloqueio imediato)
        foreach (var regra in _regrasSincronas)
        {
            if (await regra.Avaliar(transacao))
            {
                transacao.Status = StatusTransacao.Bloqueada;
                transacao.Motivo = regra.Nome;
                await _transacaoRepository.AdicionarAsync(transacao);
                return (transacao, IsDuplicata: false);
            }
        }

        // Passou nas regras síncronas: aceita provisoriamente
        transacao.Status = StatusTransacao.AceitaProvisoria;
        await _transacaoRepository.AdicionarAsync(transacao);

        // Envia para análise assíncrona
        await _filaTransacao.EnviarParaAnaliseAsync(transacao.Id);

        return (transacao, IsDuplicata: false);
    }

    public async Task<Transacao?> ConsultarStatusAsync(string transacaoId)
    {
        return await _transacaoRepository.ObterPorIdAsync(transacaoId);
    }

    public async Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync()
    {
        return await _transacaoRepository.ListarSuspeitasAsync();
    }
}
