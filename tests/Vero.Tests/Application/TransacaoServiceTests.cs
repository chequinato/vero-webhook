using Moq;
using Vero.Application.Models;
using Vero.Application.Services;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;

namespace Vero.Tests.Application;

public class TransacaoServiceTests
{
    private readonly Mock<ITransacaoRepository> _transacaoRepoMock = new();
    private readonly Mock<IContaRepository> _contaRepoMock = new();
    private readonly Mock<IFilaTransacao> _filaMock = new();
    private readonly Mock<IRiskScoringService> _riskScoringMock = new();
    private readonly Mock<INotificadorTransacao> _notificadorMock = new();
    private readonly TransacaoService _service;

    private static readonly Conta ContaRemetente = new()
    {
        Id = 1, NumeroConta = "conta_123", Titular = "Maria", Score = 85
    };

    private static readonly Conta ContaDestinatario = new()
    {
        Id = 2, NumeroConta = "conta_456", Titular = "João", Score = 72
    };

    public TransacaoServiceTests()
    {
        _contaRepoMock.Setup(r => r.ObterPorNumeroContaAsync("conta_123")).ReturnsAsync(ContaRemetente);
        _contaRepoMock.Setup(r => r.ObterPorNumeroContaAsync("conta_456")).ReturnsAsync(ContaDestinatario);

        // Risk scoring padrão: 0.15 (baixo risco)
        _riskScoringMock.Setup(r => r.CalcularRiscoAsync(
                It.IsAny<Transacao>(), It.IsAny<Conta?>(), It.IsAny<int>()))
            .ReturnsAsync(0.15f);

        // Sem regras síncronas por padrão — testes individuais adicionam quando necessário
        _service = CriarService([]);
    }

    private TransacaoService CriarService(IEnumerable<IRegraSincrona> regras)
    {
        var calculadora = new CalculadoraDeRisco(_transacaoRepoMock.Object, _riskScoringMock.Object);

        return new TransacaoService(
            _transacaoRepoMock.Object,
            _contaRepoMock.Object,
            _filaMock.Object,
            calculadora,
            _notificadorMock.Object,
            regras);
    }

    private static NovaTransacao CriarNovaTransacao(string id = "txn_001", decimal valor = 1500m) =>
        new(id, valor, "conta_123", "conta_456", "pix", "BRL", DateTime.UtcNow);

    private static Mock<IRegraSincrona> CriarRegra(string nome, bool violada)
    {
        var regra = new Mock<IRegraSincrona>();
        regra.Setup(r => r.Nome).Returns(nome);
        regra.Setup(r => r.Avaliar(It.IsAny<Transacao>())).ReturnsAsync(violada);
        return regra;
    }

    // ──────────────────────────────────────────────────────────
    // Processamento normal (sem regras violadas)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_TransacaoNormal_DeveAceitarProvisoriamente()
    {
        var resultado = await _service.ProcessarTransacaoAsync(CriarNovaTransacao());

        Assert.Equal(DesfechoRecebimento.Aceita, resultado.Desfecho);
        Assert.Equal(StatusTransacao.AceitaProvisoria, resultado.Transacao!.Status);
        Assert.Null(resultado.Transacao.Motivo);
    }

    [Fact]
    public async Task ProcessarTransacao_DeveResolverContasPeloNumero()
    {
        var resultado = await _service.ProcessarTransacaoAsync(CriarNovaTransacao());

        Assert.Equal(ContaRemetente.Id, resultado.Transacao!.RemetenteId);
        Assert.Equal(ContaDestinatario.Id, resultado.Transacao.DestinatarioId);
    }

    [Fact]
    public async Task ProcessarTransacao_TransacaoNormal_DevePersistirEEnviarParaFila()
    {
        var nova = CriarNovaTransacao();

        await _service.ProcessarTransacaoAsync(nova);

        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Once);
        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(nova.Id), Times.Once);
    }

    [Fact]
    public async Task ProcessarTransacao_DeveGuardarRiskScore()
    {
        var resultado = await _service.ProcessarTransacaoAsync(CriarNovaTransacao());

        Assert.Equal(0.15f, resultado.Transacao!.RiskScore);
    }

    [Fact]
    public async Task ProcessarTransacao_DevePassarVelocityParaRiskScoring()
    {
        _transacaoRepoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
                ContaRemetente.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(5);

        await _service.ProcessarTransacaoAsync(CriarNovaTransacao());

        _riskScoringMock.Verify(r => r.CalcularRiscoAsync(
            It.IsAny<Transacao>(), ContaRemetente, 5), Times.Once);
    }

    [Fact]
    public async Task ProcessarTransacao_DeveNotificarDashboard()
    {
        var nova = CriarNovaTransacao();

        await _service.ProcessarTransacaoAsync(nova);

        _notificadorMock.Verify(n => n.TransacaoRecebidaAsync(
            It.Is<Transacao>(t => t.Id == nova.Id)), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Conta não encontrada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_ContaInexistente_NaoDevePersistir()
    {
        var nova = CriarNovaTransacao() with { NumeroContaRemetente = "conta_inexistente" };

        var resultado = await _service.ProcessarTransacaoAsync(nova);

        Assert.Equal(DesfechoRecebimento.ContaNaoEncontrada, resultado.Desfecho);
        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Never);
        _notificadorMock.Verify(n => n.TransacaoRecebidaAsync(It.IsAny<Transacao>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Regra síncrona violada → bloqueio imediato
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_DeveBloquear()
    {
        var service = CriarService([CriarRegra("regra_teste", violada: true).Object]);

        var resultado = await service.ProcessarTransacaoAsync(CriarNovaTransacao());

        Assert.Equal(DesfechoRecebimento.Bloqueada, resultado.Desfecho);
        Assert.Equal(StatusTransacao.Bloqueada, resultado.Transacao!.Status);
        Assert.Equal("regra_teste", resultado.Transacao.Motivo);
    }

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_NaoDeveEnviarParaFila()
    {
        var service = CriarService([CriarRegra("bloqueio", violada: true).Object]);

        await service.ProcessarTransacaoAsync(CriarNovaTransacao());

        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarTransacao_RegraViolada_DevePersistirComStatusBloqueada()
    {
        var service = CriarService([CriarRegra("bloqueio", violada: true).Object]);

        await service.ProcessarTransacaoAsync(CriarNovaTransacao());

        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.Is<Transacao>(
            t => t.Status == StatusTransacao.Bloqueada)), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // Proteção contra replay
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_Duplicada_DeveRetornarExistenteSemReprocessar()
    {
        var existente = new Transacao { Id = "txn_001", Status = StatusTransacao.Aprovada };
        _transacaoRepoMock.Setup(r => r.ObterPorIdAsync(existente.Id)).ReturnsAsync(existente);

        var resultado = await _service.ProcessarTransacaoAsync(CriarNovaTransacao(existente.Id));

        Assert.Equal(DesfechoRecebimento.Duplicada, resultado.Desfecho);
        Assert.Same(existente, resultado.Transacao);
        _transacaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Never);
        _filaMock.Verify(f => f.EnviarParaAnaliseAsync(It.IsAny<string>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Múltiplas regras — para na primeira violada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessarTransacao_MultiplasRegras_DeveParaNaPrimeiraViolada()
    {
        var regra3 = CriarRegra("regra_3", violada: true);
        var service = CriarService(
        [
            CriarRegra("regra_1", violada: false).Object,
            CriarRegra("regra_2", violada: true).Object,
            regra3.Object
        ]);

        var resultado = await service.ProcessarTransacaoAsync(CriarNovaTransacao());

        Assert.Equal("regra_2", resultado.Transacao!.Motivo);
        regra3.Verify(r => r.Avaliar(It.IsAny<Transacao>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────
    // Consulta e listagem
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ConsultarStatus_TransacaoExiste_DeveRetornar()
    {
        var transacao = new Transacao { Id = "txn_001" };
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
        var suspeitas = new List<Transacao> { new() { Id = "txn_001" } }.AsReadOnly();
        _transacaoRepoMock.Setup(r => r.ListarSuspeitasAsync()).ReturnsAsync(suspeitas);

        var resultado = await _service.ListarSuspeitasAsync();

        Assert.Single(resultado);
        _transacaoRepoMock.Verify(r => r.ListarSuspeitasAsync(), Times.Once);
    }
}
