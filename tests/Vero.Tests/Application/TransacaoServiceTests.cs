using Moq;
using Vero.Application.Services;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Tests.Application;

public class TransacaoServiceTests
{
    private readonly Mock<ITransacaoRepository> _transacaoRepoMock;
    private readonly Mock<IContaRepository> _contaRepoMock;
    private readonly Mock<IFilaTransacao> _filaMock;
    private readonly TransacaoService _service;

    public TransacaoServiceTests()
    {
        _transacaoRepoMock = new Mock<ITransacaoRepository>();
        _contaRepoMock = new Mock<IContaRepository>();
        _filaMock = new Mock<IFilaTransacao>();

        // Sem regras síncronas por padrão — testes individuais adicionam quando necessário
        _service = CriarService([]);
    }

    private TransacaoService CriarService(IEnumerable<IRule> regras)
    {
        return new TransacaoService(
            _transacaoRepoMock.Object,
            _contaRepoMock.Object,
            _filaMock.Object,
            regras);
    }

    private static Transacao CriarTransacao(string id = "txn_001", decimal valor = 1500m)
    {
        return new Transacao
        {
            Id = id,
            RemetenteId = 1,
            DestinatarioId = 2,
            Valor = valor,
            Tipo = "pix",
            Moeda = "BRL",
            Timestamp = DateTime.UtcNow
        };
    }

    // ──────────────────────────────────────────────────────────
    // Processamento normal (sem regras violadas)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_TransacaoNormal_DeveAceitarProvisoriamente()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        var (resultado, isDuplicata) = await _service.ProcessarTransacaoAsync(transacao);

        Assert.Equal(StatusTransacao.AceitaProvisoria, resultado.Status);
        Assert.Null(resultado.Motivo);
        Assert.False(isDuplicata);
    }

    [Fact]
    public async Task ProcessarTransacao_TransacaoNormal_DevePersistirNoBanco()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        await _service.ProcessarTransacaoAsync(transacao);

        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Once);
    }

    [Fact]
    public async Task ProcessarTransacao_TransacaoNormal_DeveEnviarParaFila()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        await _service.ProcessarTransacaoAsync(transacao);

        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(transacao.Id), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Regra síncrona violada → bloqueio imediato
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_DeveBloquear()
    {
        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Nome).Returns("regra_teste");
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);

        var service = CriarService([regraMock.Object]);
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        var (resultado, isDuplicata) = await service.ProcessarTransacaoAsync(transacao);

        Assert.Equal(StatusTransacao.Bloqueada, resultado.Status);
        Assert.Equal("regra_teste", resultado.Motivo);
        Assert.False(isDuplicata);
    }

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_NaoDeveEnviarParaFila()
    {
        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regraMock.Setup(r => r.Nome).Returns("bloqueio");

        var service = CriarService([regraMock.Object]);
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        await service.ProcessarTransacaoAsync(transacao);

        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_DevePersistirComStatusBloqueada()
    {
        var regraMock = new Mock<IRule>();
        regraMock.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regraMock.Setup(r => r.Nome).Returns("bloqueio");

        var service = CriarService([regraMock.Object]);
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        await service.ProcessarTransacaoAsync(transacao);

        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.Is<Transacao>(
            t => t.Status == StatusTransacao.Bloqueada)), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Proteção contra replay
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_Duplicada_DeveRetornarExistenteComFlag()
    {
        var existente = CriarTransacao();
        existente.Status = StatusTransacao.Aprovada;

        _transacaoRepoMock.Setup(r => r.ExisteAsync(existente.Id)).ReturnsAsync(true);
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(existente.Id)).ReturnsAsync(existente);

        var novaTransacao = CriarTransacao();
        var (resultado, isDuplicata) = await _service.ProcessarTransacaoAsync(novaTransacao);

        Assert.True(isDuplicata);
        Assert.Equal(StatusTransacao.Aprovada, resultado.Status);
        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Never);
        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(It.IsAny<string>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Múltiplas regras — para na primeira violada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_MultiplasRegras_DeveParaNaPrimeiraViolada()
    {
        var regra1 = new Mock<IRule>();
        regra1.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(false);
        regra1.Setup(r => r.Nome).Returns("regra_1");

        var regra2 = new Mock<IRule>();
        regra2.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(true);
        regra2.Setup(r => r.Nome).Returns("regra_2");

        var regra3 = new Mock<IRule>();
        regra3.Setup(r => r.Nome).Returns("regra_3");

        var service = CriarService([regra1.Object, regra2.Object, regra3.Object]);
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ExisteAsync(transacao.Id)).ReturnsAsync(false);

        var (resultado, _) = await service.ProcessarTransacaoAsync(transacao);

        Assert.Equal("regra_2", resultado.Motivo);
        regra3.Verify(r => r.Avaliar(It.IsAny<Transacao>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Consulta e listagem
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsultarStatus_TransacaoExiste_DeveRetornar()
    {
        var transacao = CriarTransacao();
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(transacao.Id)).ReturnsAsync(transacao);

        var resultado = await _service.ConsultarStatusAsync(transacao.Id);

        Assert.NotNull(resultado);
        Assert.Equal(transacao.Id, resultado.Id);
    }

    [Fact]
    public async Task ConsultarStatus_TransacaoNaoExiste_DeveRetornarNull()
    {
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync("inexistente")).ReturnsAsync((Transacao?)null);

        var resultado = await _service.ConsultarStatusAsync("inexistente");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ListarSuspeitas_DeveDelegarAoRepositorio()
    {
        var suspeitas = new List<Transacao> { CriarTransacao() }.AsReadOnly();
        _transacaoRepoMock.Setup(r => r.ListarSuspeitasAsync()).ReturnsAsync(suspeitas);

        var resultado = await _service.ListarSuspeitasAsync();

        Assert.Single(resultado);
        _transacaoRepoMock.Verify(r => r.ListarSuspeitasAsync(), Times.Once);
    }
}
