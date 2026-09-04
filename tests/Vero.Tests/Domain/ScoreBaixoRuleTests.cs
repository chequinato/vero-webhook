using Moq;
using Vero.Domain.Entities;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules.Sync;

namespace Vero.Tests.Domain;

public class ScoreBaixoRuleTests
{
    private readonly Mock<IContaRepository> _contaRepoMock;
    private readonly ScoreBaixoRule _rule;

    public ScoreBaixoRuleTests()
    {
        _contaRepoMock = new Mock<IContaRepository>();
        _rule = new ScoreBaixoRule(_contaRepoMock.Object);
    }

    [Fact]
    public async Task DeveBloquear_QuandoScoreAbaixoDe35()
    {
        var conta = new Conta { Id = 1, Score = 20 };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(conta);

        var transacao = new Transacao { Id = "txn_001", RemetenteId = 1 };
        var resultado = await _rule.Avaliar(transacao);

        Assert.True(resultado);
    }

    [Fact]
    public async Task NaoDeveBloquear_QuandoScoreAcimaDe35()
    {
        var conta = new Conta { Id = 1, Score = 80 };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(conta);

        var transacao = new Transacao { Id = "txn_001", RemetenteId = 1 };
        var resultado = await _rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public async Task NaoDeveBloquear_QuandoScoreExatamente35()
    {
        var conta = new Conta { Id = 1, Score = ScoreBaixoRule.ScoreMinimo };
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(1)).ReturnsAsync(conta);

        var transacao = new Transacao { Id = "txn_001", RemetenteId = 1 };
        var resultado = await _rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public async Task NaoDeveBloquear_QuandoContaNaoExiste()
    {
        _contaRepoMock.Setup(r => r.ObterPorIdAsync(99)).ReturnsAsync((Conta?)null);

        var transacao = new Transacao { Id = "txn_001", RemetenteId = 99 };
        var resultado = await _rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public void Nome_DeveSerScoreBaixo()
    {
        Assert.Equal("score_baixo", _rule.Nome);
    }
}
