using Moq;
using Vero.Domain.Entities;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules.Async;

namespace Vero.Tests.Domain;

public class VelocityRuleTests
{
    private readonly Mock<ITransacaoRepository> _repoMock;
    private readonly VelocityRule _rule;

    public VelocityRuleTests()
    {
        _repoMock = new Mock<ITransacaoRepository>();
        _rule = new VelocityRule(_repoMock.Object);
    }

    [Fact]
    public async Task DeveDetectarVelocity_QuandoMuitasTransacoesNaJanela()
    {
        var transacao = new Transacao
        {
            Id = "txn_001",
            RemetenteId = 1,
            Timestamp = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
            1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(VelocityRule.LimiteTransacoes); // exatamente no limite

        var resultado = await _rule.Avaliar(transacao);

        Assert.True(resultado);
    }

    [Fact]
    public async Task NaoDeveDetectarVelocity_QuandoPoucasTransacoes()
    {
        var transacao = new Transacao
        {
            Id = "txn_001",
            RemetenteId = 1,
            Timestamp = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
            1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(1);

        var resultado = await _rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public async Task NaoDeveDetectarVelocity_QuandoNenhumaTransacao()
    {
        var transacao = new Transacao
        {
            Id = "txn_001",
            RemetenteId = 1,
            Timestamp = DateTime.UtcNow
        };

        _repoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
            1, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var resultado = await _rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public void Nome_DeveSerVelocity()
    {
        Assert.Equal("velocity", _rule.Nome);
    }
}
