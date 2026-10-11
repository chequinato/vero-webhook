using Microsoft.AspNetCore.Mvc;
using Moq;
using Vero.Api.Controllers;
using Vero.Api.DTOs.Requests;
using Vero.Api.DTOs.Responses;
using Vero.Application.Interfaces;
using Vero.Application.Models;
using Vero.Domain.Entities;
using Vero.Domain.Enums;

namespace Vero.Tests.Api;

/// <summary>
/// O controller agora só depende de ITransacaoService, então o teste só
/// precisa de um mock — compare com a versão antiga, que mockava repositórios,
/// ML e SignalR para testar um controller. Isso é o SRP aparecendo nos testes.
/// </summary>
public class TransactionsControllerTests
{
    private readonly Mock<ITransacaoService> _serviceMock = new();
    private readonly TransactionsController _controller;

    public TransactionsControllerTests()
    {
        _controller = new TransactionsController(_serviceMock.Object);
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

    private void ServiceRetorna(ResultadoRecebimento resultado)
    {
        _serviceMock.Setup(s => s.ProcessarTransacaoAsync(It.IsAny<NovaTransacao>()))
            .ReturnsAsync(resultado);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — aceita provisoriamente
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_TransacaoNormal_DeveRetornar202ComRiskScore()
    {
        var request = CriarRequest();
        ServiceRetorna(ResultadoRecebimento.Aceita(new Transacao
        {
            Id = request.Id, Status = StatusTransacao.AceitaProvisoria, RiskScore = 0.15f
        }));

        var result = await _controller.ReceberTransacao(request);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var dto = Assert.IsType<TransacaoResponseDto>(accepted.Value);
        Assert.Equal("aceita_provisoria", dto.Status);
        Assert.Equal(request.Id, dto.Id);
        Assert.Equal(0.15f, dto.RiskScore);
    }

    [Fact]
    public async Task Post_DeveConverterDtoParaNovaTransacao()
    {
        var request = CriarRequest();
        ServiceRetorna(ResultadoRecebimento.Aceita(new Transacao { Id = request.Id }));

        await _controller.ReceberTransacao(request);

        _serviceMock.Verify(s => s.ProcessarTransacaoAsync(It.Is<NovaTransacao>(n =>
            n.Id == request.Id &&
            n.NumeroContaRemetente == "conta_123" &&
            n.NumeroContaDestinatario == "conta_456" &&
            n.Valor == request.Valor)), Times.Once);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — bloqueada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_TransacaoBloqueada_DeveRetornar403()
    {
        var request = CriarRequest(valor: 200_000m);
        ServiceRetorna(ResultadoRecebimento.Bloqueada(new Transacao
        {
            Id = request.Id, Status = StatusTransacao.Bloqueada, Motivo = "valor_alto"
        }));

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
        ServiceRetorna(ResultadoRecebimento.Duplicada(new Transacao
        {
            Id = request.Id, Status = StatusTransacao.Aprovada
        }));

        var result = await _controller.ReceberTransacao(request);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var dto = Assert.IsType<TransacaoResponseDto>(conflict.Value);
        Assert.Equal("transacao_duplicada", dto.Motivo);
        Assert.Equal("aprovada", dto.Status);
    }

    // ──────────────────────────────────────────────────────────
    // POST /transactions — conta não encontrada
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ContaInexistente_DeveRetornar400()
    {
        ServiceRetorna(ResultadoRecebimento.ContaNaoEncontrada());

        var result = await _controller.ReceberTransacao(CriarRequest());

        Assert.IsType<BadRequestObjectResult>(result);
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
