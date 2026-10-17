using Moq;
using Vero.Application.Models;
using Vero.Application.Services;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Policies;

namespace Vero.Tests.Application;

public class ReavaliacaoServiceTests
{
    private readonly Mock<ITransacaoRepository> _transacaoRepoMock = new();
    private readonly Mock<IContaRepository> _contaRepoMock = new();
    private readonly Mock<IRiskScoringService> _riskScoringMock = new();
    private readonly Mock<INotificadorTransacao> _notificadorMock = new();
    private readonly ReavaliacaoService _service;

    public ReavaliacaoServiceTests()
    {
        var calculadora = new CalculadoraDeRisco(_transacaoRepoMock.Object, _riskScoringMock.Object);

        _service = new ReavaliacaoService(
            _transacaoRepoMock.Object,
            _contaRepoMock.Object,
            calculadora,
            _notificadorMock.Object);
    }

    private Transacao TransacaoNoBanco(StatusTransacao status)
    {
        var transacao = new Transacao
        {
            Id = "txn_001", RemetenteId = 1, Status = status, Timestamp = DateTime.UtcNow
        };
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);
        return transacao;
    }

    private void ModeloRetorna(float score)
    {
        _riskScoringMock.Setup(r => r.CalcularRiscoAsync(
                It.IsAny<Transacao>(), It.IsAny<Conta?>(), It.IsAny<int>()))
            .ReturnsAsync(score);
    }

    [Fact]
    public async Task Reavaliar_TransacaoInexistente_DeveRetornarNaoEncontrada()
    {
        var resultado = await _service.ReavaliarAsync("nao_existe");

        Assert.Equal(DesfechoReavaliacao.NaoEncontrada, resultado.Desfecho);
    }

    [Theory]
    [InlineData(StatusTransacao.Aprovada)]
    [InlineData(StatusTransacao.Bloqueada)]
    public async Task Reavaliar_JaDecidida_NaoDeveChamarModelo(StatusTransacao status)
    {
        TransacaoNoBanco(status);

        var resultado = await _service.ReavaliarAsync("txn_001");

        Assert.Equal(DesfechoReavaliacao.JaDecidida, resultado.Desfecho);
        _riskScoringMock.Verify(r => r.CalcularRiscoAsync(
            It.IsAny<Transacao>(), It.IsAny<Conta?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Reavaliar_ScoreAcimaDoCorte_DeveBloquearEPersistirScore()
    {
        TransacaoNoBanco(StatusTransacao.Suspeita);
        ModeloRetorna(0.91f);

        var resultado = await _service.ReavaliarAsync("txn_001");

        Assert.Equal(DesfechoReavaliacao.Reavaliada, resultado.Desfecho);
        Assert.Equal(StatusTransacao.Bloqueada, resultado.Transacao!.Status);
        _transacaoRepoMock.Verify(r => r.AtualizarStatusEScoreAsync(
            "txn_001", StatusTransacao.Bloqueada, PoliticaDeRisco.MotivoBloqueioModelo, 0.91f), Times.Once);
    }

    [Fact]
    public async Task Reavaliar_ScoreAbaixoDoCorte_DeveAprovar()
    {
        TransacaoNoBanco(StatusTransacao.AceitaProvisoria);
        ModeloRetorna(0.20f);

        var resultado = await _service.ReavaliarAsync("txn_001");

        Assert.Equal(StatusTransacao.Aprovada, resultado.Transacao!.Status);
        Assert.Null(resultado.Transacao.Motivo);
    }

    [Fact]
    public async Task Reavaliar_DeveNotificarDashboard()
    {
        TransacaoNoBanco(StatusTransacao.Suspeita);
        ModeloRetorna(0.20f);

        await _service.ReavaliarAsync("txn_001");

        _notificadorMock.Verify(n => n.StatusAtualizadoAsync(
            It.Is<Transacao>(t => t.Id == "txn_001" && t.RiskScore == 0.20f)), Times.Once);
    }
}
