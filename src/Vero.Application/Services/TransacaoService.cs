using Vero.Application.Interfaces;
using Vero.Application.Models;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Application.Services;

/// <summary>
/// Orquestra o recebimento de transações: resolve contas, calcula o risco,
/// aplica regras síncronas, persiste, enfileira para análise assíncrona e
/// notifica o dashboard.
///
/// Toda essa orquestração vivia no TransactionsController. Agora o controller
/// só traduz HTTP ↔ Application, e esta classe pode ser testada sem subir
/// ASP.NET (Single Responsibility + Dependency Inversion).
/// </summary>
public class TransacaoService : ITransacaoService
{
    private readonly ITransacaoRepository _transacaoRepository;
    private readonly IContaRepository _contaRepository;
    private readonly IFilaTransacao _filaTransacao;
    private readonly CalculadoraDeRisco _calculadoraDeRisco;
    private readonly INotificadorTransacao _notificador;
    private readonly IEnumerable<IRegraSincrona> _regrasSincronas;

    public TransacaoService(
        ITransacaoRepository transacaoRepository,
        IContaRepository contaRepository,
        IFilaTransacao filaTransacao,
        CalculadoraDeRisco calculadoraDeRisco,
        INotificadorTransacao notificador,
        IEnumerable<IRegraSincrona> regrasSincronas)
    {
        _transacaoRepository = transacaoRepository;
        _contaRepository = contaRepository;
        _filaTransacao = filaTransacao;
        _calculadoraDeRisco = calculadoraDeRisco;
        _notificador = notificador;
        _regrasSincronas = regrasSincronas;
    }

    public async Task<ResultadoRecebimento> ProcessarTransacaoAsync(NovaTransacao nova)
    {
        var remetente = await _contaRepository.ObterPorNumeroContaAsync(nova.NumeroContaRemetente);
        var destinatario = await _contaRepository.ObterPorNumeroContaAsync(nova.NumeroContaDestinatario);

        if (remetente is null || destinatario is null)
            return ResultadoRecebimento.ContaNaoEncontrada();

        // Proteção contra replay: se a transação já existe, não processa de novo
        var existente = await _transacaoRepository.ObterPorIdAsync(nova.Id);
        if (existente is not null)
            return ResultadoRecebimento.Duplicada(existente);

        var transacao = new Transacao
        {
            Id = nova.Id,
            RemetenteId = remetente.Id,
            DestinatarioId = destinatario.Id,
            Valor = nova.Valor,
            Tipo = nova.Tipo,
            Moeda = nova.Moeda,
            Timestamp = nova.Timestamp
        };

        transacao.RiskScore = await _calculadoraDeRisco.CalcularAsync(transacao, remetente, DateTime.UtcNow);

        var resultado = await AplicarRegrasSincronasAsync(transacao);

        await _notificador.TransacaoRecebidaAsync(transacao);

        return resultado;
    }

    private async Task<ResultadoRecebimento> AplicarRegrasSincronasAsync(Transacao transacao)
    {
        var regraViolada = await _regrasSincronas.PrimeiraVioladaAsync(transacao);

        if (regraViolada is not null)
        {
            transacao.Status = StatusTransacao.Bloqueada;
            transacao.Motivo = regraViolada.Nome;
            await _transacaoRepository.AdicionarAsync(transacao);
            return ResultadoRecebimento.Bloqueada(transacao);
        }

        // Passou nas regras síncronas: aceita provisoriamente e manda para o Worker
        transacao.Status = StatusTransacao.AceitaProvisoria;
        await _transacaoRepository.AdicionarAsync(transacao);
        await _filaTransacao.EnviarParaAnaliseAsync(transacao.Id);
        return ResultadoRecebimento.Aceita(transacao);
    }

    public Task<Transacao?> ConsultarStatusAsync(string transacaoId) =>
        _transacaoRepository.ObterPorIdAsync(transacaoId);

    public Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync() =>
        _transacaoRepository.ListarSuspeitasAsync();
}
