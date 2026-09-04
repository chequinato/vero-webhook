using Moq;
using Vero.Application.Services;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Tests.Application;

public class AnomalyDetectionServiceTests
{
    private readonly Mock<ITransacaoRepository> _transacaoRepoMock;
    private readonly Mock<IAlertaService> _alertaMock;

    public AnomalyDetectionServiceTests()
    {
        _transacaoRepoMock = new Mock<ITransacaoRepository>();
        _alertaMock = new Mock<IAlertaService>();
    }

    private AnomalyDetectionService CriarService(IEnumerable<IRule> regras)
    {
        return new AnomalyDetectionService(
            _transacaoRepoMock.Object,
            _alertaMock.Object,
            regras);
    }

    private static Transacao CriarTransacao(
        string id = "txn_001",
        StatusTransacao status = StatusTransacao.AceitaProvisoria)
    {
        return new Transacao
        {
            Id = id,
            RemetenteId = 1,
            DestinatarioId = 2,
            Valor = 5000m,
            Tipo = "pix",
            Moeda = "BRL",
            Timestamp = DateTime.UtcNow,
            Status = status
        };
    }

    // ──────────────────────────────────────────────────────────
    // Análise sem anomalias → aprovada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Analisar_SemAnomalias_DeveAprovar()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var service = CriarService([]);
        await service.AnalisarAsync(transacao.Id);

        _transacaoRepoMock.Verify(r => r.AtualizarStatusAsync(
            transacao.Id, StatusTransacao.Aprovada, null), Times.Once);
    }

    [Fact]
    public async Task Analisar_SemAnomalias_NaoDeveEnviarAlerta()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var service = CriarService([]);
        await service.AnalisarAsync(transacao.Id);

        _alertaMock.Verify(a => a.EnviarAlertaAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Anomalia encontrada → suspeita + alerta
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Analisar_AnomaliaEncontrada_DeveMarcarComoSuspeita()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regraMock.Setup(r => r.Nome).Returns("velocity");

        var service = CriarService([regraMock.Object]);
        await service.AnalisarAsync(transacao.Id);

        _transacaoRepoMock.Verify(r => r.AtualizarStatusAsync(
            transacao.Id, StatusTransacao.Suspeita, "velocity"), Times.Once);
    }

    [Fact]
    public async Task Analisar_AnomaliaEncontrada_DeveEnviarAlerta()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regraMock.Setup(r => r.Nome).Returns("horario_estranho");

        var service = CriarService([regraMock.Object]);
        await service.AnalisarAsync(transacao.Id);

        _alertaMock.Verify(a => a.EnviarAlertaAsync(
            transacao.Id, "horario_estranho"), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Transação não encontrada → ignora silenciosamente
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Analisar_TransacaoNaoExiste_NaoDeveFazerNada()
    {
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync("inexistente")).ReturnsAsync((Transacao?)null);

        var service = CriarService([]);
        await service.AnalisarAsync("inexistente");

        _transacaoRepoMock.Verify(r => r.AtualizarStatusAsync(
            It.IsAny<string>(), It.IsAny<StatusTransacao>(), It.IsAny<string?>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Status diferente de AceitaProvisoria → ignora
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StatusTransacao.Bloqueada)]
    [InlineData(StatusTransacao.Aprovada)]
    [InlineData(StatusTransacao.Suspeita)]
    public async Task Analisar_StatusNaoEhAceitaProvisoria_NaoDeveProcessar(StatusTransacao status)
    {
        var transacao = CriarTransacao(status: status);
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regraMock.Setup(r => r.Nome).Returns("qualquer");

        var service = CriarService([regraMock.Object]);
        await service.AnalisarAsync(transacao.Id);

        _transacaoRepoMock.Verify(r => r.AtualizarStatusAsync(
            It.IsAny<string>(), It.IsAny<StatusTransacao>(), It.IsAny<string?>()), Times.Never);
        regraMock.Verify(r => r.Avaliar(It.IsAny<Transacao>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Múltiplas regras — para na primeira anomalia
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Analisar_MultiplasRegras_DeveParaNaPrimeiraAnomalia()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var regra1 = new Mock<IRule>();
        regra1.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(false);
        regra1.Setup(r => r.Nome).Returns("regra_ok");

        var regra2 = new Mock<IRule>();
        regra2.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regra2.Setup(r => r.Nome).Returns("regra_violada");

        var regra3 = new Mock<IRule>();
        regra3.Setup(r => r.Nome).Returns("regra_nunca_chega");

        var service = CriarService([regra1.Object, regra2.Object, regra3.Object]);
        await service.AnalisarAsync(transacao.Id);

        _transacaoRepoMock.Verify(r => r.AtualizarStatusAsync(
            transacao.Id, StatusTransacao.Suspeita, "regra_violada"), Times.Once);
        regra3.Verify(r => r.Avaliar(It.IsAny<Transacao>()), Times.Never);
    }
}
