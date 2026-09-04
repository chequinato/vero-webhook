using Microsoft.EntityFrameworkCore;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Repositories;

namespace Vero.Tests.Infrastructure;

public class TransacaoRepositoryTests : IDisposable
{
    private readonly VeroDbContext _context;
    private readonly TransacaoRepository _repository;

    public TransacaoRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<VeroDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new VeroDbContext(options);
        _repository = new TransacaoRepository(_context);

        // Seed de contas
        _context.Contas.AddRange(
            new Conta { Id = 1, NumeroConta = "conta_123", Titular = "Maria", Score = 85 },
            new Conta { Id = 2, NumeroConta = "conta_456", Titular = "João", Score = 72 }
        );
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private Transacao CriarTransacao(
        string id = "txn_001",
        StatusTransacao status = StatusTransacao.AceitaProvisoria,
        decimal valor = 1500m)
    {
        return new Transacao
        {
            Id = id,
            RemetenteId = 1,
            DestinatarioId = 2,
            Valor = valor,
            Tipo = "pix",
            Moeda = "BRL",
            Timestamp = DateTime.UtcNow,
            Status = status
        };
    }

    // ──────────────────────────────────────────────────────────
    // Adicionar e consultar
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AdicionarAsync_DevePersistirTransacao()
    {
        var transacao = CriarTransacao();

        await _repository.AdicionarAsync(transacao);
        var resultado = await _repository.ObterPorIdAsync(transacao.Id);

        Assert.NotNull(resultado);
        Assert.Equal(transacao.Id, resultado.Id);
        Assert.Equal(StatusTransacao.AceitaProvisoria, resultado.Status);
    }

    [Fact]
    public async Task AdicionarAsync_DeveCriarHistoricoDeStatus()
    {
        var transacao = CriarTransacao();

        await _repository.AdicionarAsync(transacao);

        var historico = await _context.TransacaoHistoricoStatus
            .Where(h => h.TransacaoId == transacao.Id)
            .ToListAsync();

        Assert.Single(historico);
        Assert.Null(historico[0].StatusAnterior);
        Assert.Equal("AceitaProvisoria", historico[0].StatusNovo);
    }

    // ──────────────────────────────────────────────────────────
    // ExisteAsync
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExisteAsync_TransacaoExistente_DeveRetornarTrue()
    {
        await _repository.AdicionarAsync(CriarTransacao("txn_existe"));

        var resultado = await _repository.ExisteAsync("txn_existe");

        Assert.True(resultado);
    }

    [Fact]
    public async Task ExisteAsync_TransacaoInexistente_DeveRetornarFalse()
    {
        var resultado = await _repository.ExisteAsync("txn_nao_existe");

        Assert.False(resultado);
    }

    // ──────────────────────────────────────────────────────────
    // AtualizarStatusAsync
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AtualizarStatus_DeveAlterarStatusECriarHistorico()
    {
        var transacao = CriarTransacao();
        await _repository.AdicionarAsync(transacao);

        await _repository.AtualizarStatusAsync(transacao.Id, StatusTransacao.Aprovada, null);

        var atualizada = await _repository.ObterPorIdAsync(transacao.Id);
        Assert.Equal(StatusTransacao.Aprovada, atualizada!.Status);

        var historico = await _context.TransacaoHistoricoStatus
            .Where(h => h.TransacaoId == transacao.Id)
            .OrderBy(h => h.AlteradoEm)
            .ToListAsync();

        Assert.Equal(2, historico.Count);
        Assert.Equal("AceitaProvisoria", historico[1].StatusAnterior);
        Assert.Equal("Aprovada", historico[1].StatusNovo);
    }

    // ──────────────────────────────────────────────────────────
    // ListarSuspeitasAsync
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarSuspeitas_DeveRetornarApenasSuspeitasEBloqueadas()
    {
        await _repository.AdicionarAsync(CriarTransacao("txn_1", StatusTransacao.AceitaProvisoria));
        await _repository.AdicionarAsync(CriarTransacao("txn_2", StatusTransacao.Bloqueada));
        await _repository.AdicionarAsync(CriarTransacao("txn_3", StatusTransacao.Suspeita));
        await _repository.AdicionarAsync(CriarTransacao("txn_4", StatusTransacao.Aprovada));

        var resultado = await _repository.ListarSuspeitasAsync();

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, t =>
            Assert.True(t.Status == StatusTransacao.Bloqueada || t.Status == StatusTransacao.Suspeita));
    }

    // ──────────────────────────────────────────────────────────
    // ContarPorRemetenteNoPeriodoAsync (velocity)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ContarPorRemetente_DeveContarApenasNoPeriodo()
    {
        var agora = DateTime.UtcNow;

        // 3 transações nos últimos 5 minutos
        for (int i = 1; i <= 3; i++)
        {
            var tx = CriarTransacao($"txn_{i}");
            tx.Timestamp = agora.AddMinutes(-i);
            await _repository.AdicionarAsync(tx);
        }

        // 1 transação de 1 hora atrás (fora da janela)
        var txAntiga = CriarTransacao("txn_antiga");
        txAntiga.Timestamp = agora.AddHours(-1);
        await _repository.AdicionarAsync(txAntiga);

        var contagem = await _repository.ContarPorRemetenteNoPeriodoAsync(
            1, agora.AddMinutes(-5), agora);

        Assert.Equal(3, contagem);
    }
}
