using Microsoft.AspNetCore.Mvc;
using Moq;
using Vero.Api.Controllers;
using Vero.Api.DTOs.Requests;
using Vero.Api.DTOs.Responses;
using Vero.Api.Hubs;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Vero.Tests.Api;

public class TransactionsControllerTests
{
    private readonly Mock<ITransacaoService> _serviceMock;
    private readonly Mock<IContaRepository> _contaRepoMock;
    private readonly Mock<ITransacaoRepository> _transacaoRepoMock;
    private readonly Mock<IRiskScoringService> _riskScoringMock;
    private readonly TransactionHubNotifier _hubNotifier;
    private readonly TransactionsController _controller;

    private static readonly Conta ContaRemetente = new()
    {
        Id = 1, NumeroConta = "conta_123", Titular = "Maria", Score = 85
    };

    private static readonly Conta ContaDestinatario = new()
    {
        Id = 2, NumeroConta = "conta_456", Titular = "João", Score = 72
    };

    public TransactionsControllerTests()
    {
        _serviceMock = new Mock<ITransacaoService>();
        _contaRepoMock = new Mock<IContaRepository>();
        _transacaoRepoMock = new Mock<ITransacaoRepository>();
        _riskScoringMock = new Mock<IRiskScoringService>();

        // Mock do SignalR hub context
        var hubContextMock = new Mock<IHubContext<TransactionHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        _hubNotifier = new TransactionHubNotifier(hubContextMock.Object);

        // Risk scoring padrão: retorna 0.15 (baixo risco)
        _riskScoringMock.Setup(r => r.CalcularRiscoAsync(
                It.IsAny<Transacao>(), It.IsAny<Conta?>(), It.IsAny<int>()))
            .ReturnsAsync(0.15f);

        // Velocity padrão: 0 transações recentes
        _transacaoRepoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        _controller = new TransactionsController(
            _serviceMock.Object,
            _contaRepoMock.Object,
            _transacaoRepoMock.Object,
            _riskScoringMock.Object,
            _hubNotifier);

        // Setup padrão: contas existem
        _contaRepoMock.Setup(r => r.ObterPorNumeroContaAsync("conta_123"))
            .ReturnsAsync(ContaRemetente);
        _contaRepoMock.Setup(r => r.ObterPorNumeroContaAsync("conta_456"))
            .ReturnsAsync(ContaDestinatario);
    }

    private static TransacaoRequestDto CriarRequest(string id = "txn_001", decimal valor = 1500m)
    {
        return new TransacaoRequestDto
        {
            Id = id,
            Valor = valor,
            Remetente = "conta_123",
            Destinatario = "conta_456",
            Tipo = "pix",
            Timestamp = DateTime.UtcNow,
            Moeda = "BRL"
        };
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — aceita provisoriamente
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_TransacaoNormal_DeveRetornar202()
    {
        var request = CriarRequest();
        var transacaoResultado = new Transacao { Id = request.Id, Status = StatusTransacao.AceitaProvisoria };
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<Transacao>()))
            .ReturnsAsync((transacaoResultado, false));

        var result = await _controller.ReceberTransacao(request);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var dto = Assert.IsType<TransacaoResponseDto>(accepted.Value);
        Assert.Equal("aceita_provisoria", dto.Status);
        Assert.Equal(request.Id, dto.Id);
    }

    [Fact]
    public async Task Post_TransacaoNormal_DeveIncluirRiskScore()
    {
        var request = CriarRequest();
        var transacaoResultado = new Transacao { Id = request.Id, Status = StatusTransacao.AceitaProvisoria };
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<Transacao>()))
            .ReturnsAsync((transacaoResultado, false));

        var result = await _controller.ReceberTransacao(request);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var dto = Assert.IsType<TransacaoResponseDto>(accepted.Value);
        Assert.NotNull(dto.RiskScore);
        Assert.Equal(0.15f, dto.RiskScore);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — bloqueada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_TransacaoBloqueada_DeveRetornar403()
    {
        var request = CriarRequest(valor: 200_000m);
        var transacaoBloqueada = new Transacao
        {
            Id = request.Id,
            Status = StatusTransacao.Bloqueada,
            Motivo = "valor_alto"
        };
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<Transacao>()))
            .ReturnsAsync((transacaoBloqueada, false));

        var result = await _controller.ReceberTransacao(request);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var dto = Assert.IsType<TransacaoResponseDto>(objectResult.Value);
        Assert.Equal("bloqueada", dto.Status);
        Assert.Equal("valor_alto", dto.Motivo);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — replay (duplicada)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_TransacaoDuplicada_DeveRetornar409()
    {
        var request = CriarRequest();
        var existente = new Transacao
        {
            Id = request.Id,
            Status = StatusTransacao.Aprovada
        };
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<Transacao>()))
            .ReturnsAsync((existente, true));

        var result = await _controller.ReceberTransacao(request);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var dto = Assert.IsType<TransacaoResponseDto>(conflict.Value);
        Assert.Equal("transacao_duplicada", dto.Motivo);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — conta não encontrada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ContaInexistente_DeveRetornar400()
    {
        var request = CriarRequest();
        request.Remetente = "conta_inexistente";
        _contaRepoMock.Setup(r => r.ObterPorNumeroContaAsync("conta_inexistente"))
            .ReturnsAsync((Conta?)null);

        var result = await _controller.ReceberTransacao(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — velocity é passada ao ML
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_DevePassarVelocityParaRiskScoring()
    {
        var request = CriarRequest();
        _transacaoRepoMock.Setup(r => r.ContarPorRemetenteNoPeriodoAsync(
                ContaRemetente.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(5);

        var transacaoResultado = new Transacao { Id = request.Id, Status = StatusTransacao.AceitaProvisoria };
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<Transacao>()))
            .ReturnsAsync((transacaoResultado, false));

        await _controller.ReceberTransacao(request);

        _riskScoringMock.Verify(r => r.CalcularRiscoAsync(
            It.IsAny<Transacao>(), ContaRemetente, 5), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // GET /transactions/{id}
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_TransacaoExistente_DeveRetornar200()
    {
        var transacao = new Transacao
        {
            Id = "txn_001",
            Status = StatusTransacao.Aprovada,
            Motivo = null,
            RiskScore = 0.12f,
            CreatedAt = DateTime.UtcNow
        };
        _serviceMock.Setup(s => s.ConsultarStatusAsync("txn_001")).ReturnsAsync(transacao);

        var result = await _controller.ConsultarStatus("txn_001");

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<TransacaoStatusResponseDto>(ok.Value);
        Assert.Equal("txn_001", dto.Id);
        Assert.Equal("aprovada", dto.Status);
        Assert.Equal(0.12f, dto.RiskScore);
    }

    [Fact]
    public async Task Get_TransacaoInexistente_DeveRetornar404()
    {
        _serviceMock.Setup(s => s.ConsultarStatusAsync("nao_existe")).ReturnsAsync((Transacao?)null);

        var result = await _controller.ConsultarStatus("nao_existe");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────────────────────────────────────────────
    // GET /transactions?suspicious=true
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuspeitas_DeveRetornarListagem()
    {
        var suspeitas = new List<Transacao>
        {
            new() { Id = "txn_007", Status = StatusTransacao.Suspeita, Motivo = "velocity" },
            new() { Id = "txn_012", Status = StatusTransacao.Bloqueada, Motivo = "valor_alto" }
        }.AsReadOnly();

        _serviceMock.Setup(s => s.ListarSuspeitasAsync()).ReturnsAsync(suspeitas);

        var result = await _controller.ListarTransacoes(suspicious: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<TransacoesSuspeitasResponseDto>(ok.Value);
        Assert.Equal(2, dto.Total);
        Assert.Equal("txn_007", dto.Transacoes[0].Id);
    }

    [Fact]
    public async Task GetSuspeitas_SemFlag_DeveRetornar400()
    {
        var result = await _controller.ListarTransacoes(suspicious: false);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
