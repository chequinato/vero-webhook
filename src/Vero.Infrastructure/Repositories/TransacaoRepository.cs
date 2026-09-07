using Microsoft.EntityFrameworkCore;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;
using Vero.Infrastructure.Data;

namespace Vero.Infrastructure.Repositories;

public class TransacaoRepository : ITransacaoRepository
{
    private readonly VeroDbContext _context;

    public TransacaoRepository(VeroDbContext context)
    {
        _context = context;
    }

    public async Task<Transacao?> ObterPorIdAsync(string id)
    {
        return await _context.Transacoes.FindAsync(id);
    }

    public async Task<bool> ExisteAsync(string id)
    {
        return await _context.Transacoes.AnyAsync(t => t.Id == id);
    }

    public async Task AdicionarAsync(Transacao transacao)
    {
        await _context.Transacoes.AddAsync(transacao);

        // Registra o primeiro status no histórico
        await _context.TransacaoHistoricoStatus.AddAsync(new TransacaoHistoricoStatus
        {
            TransacaoId = transacao.Id,
            StatusAnterior = null,
            StatusNovo = transacao.Status.ToString(),
            Motivo = transacao.Motivo,
            AlteradoEm = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task AtualizarStatusAsync(string transacaoId, StatusTransacao novoStatus, string? motivo)
    {
        var transacao = await _context.Transacoes.FindAsync(transacaoId);
        if (transacao is null) return;

        var statusAnterior = transacao.Status.ToString();
        transacao.Status = novoStatus;
        transacao.Motivo = motivo;

        // Registra a mudança no histórico
        await _context.TransacaoHistoricoStatus.AddAsync(new TransacaoHistoricoStatus
        {
            TransacaoId = transacaoId,
            StatusAnterior = statusAnterior,
            StatusNovo = novoStatus.ToString(),
            Motivo = motivo,
            AlteradoEm = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task AtualizarStatusEScoreAsync(
        string transacaoId, StatusTransacao novoStatus, string? motivo, float riskScore)
    {
        var transacao = await _context.Transacoes.FindAsync(transacaoId);
        if (transacao is null) return;

        var statusAnterior = transacao.Status.ToString();
        transacao.Status = novoStatus;
        transacao.Motivo = motivo;
        transacao.RiskScore = riskScore;

        await _context.TransacaoHistoricoStatus.AddAsync(new TransacaoHistoricoStatus
        {
            TransacaoId = transacaoId,
            StatusAnterior = statusAnterior,
            StatusNovo = novoStatus.ToString(),
            Motivo = motivo,
            AlteradoEm = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync()
    {
        return await _context.Transacoes
            .Where(t => t.Status == StatusTransacao.Suspeita || t.Status == StatusTransacao.Bloqueada)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> ContarPorRemetenteNoPeriodoAsync(int remetenteId, DateTime inicio, DateTime fim)
    {
        return await _context.Transacoes
            .CountAsync(t => t.RemetenteId == remetenteId
                          && t.Timestamp >= inicio
                          && t.Timestamp <= fim);
    }

    public async Task<(IReadOnlyList<Transacao> Items, int Total)> ListarPaginadoAsync(
        int pagina, int tamanhoPagina, StatusTransacao? filtroStatus = null)
    {
        var query = _context.Transacoes.AsQueryable();

        if (filtroStatus.HasValue)
            query = query.Where(t => t.Status == filtroStatus.Value);

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync();

        return (items, total);
    }

    public async Task<TransacaoStats> ObterEstatisticasAsync()
    {
        var transacoes = _context.Transacoes;

        var total = await transacoes.CountAsync();
        if (total == 0)
        {
            return new TransacaoStats();
        }

        // Query única para contadores de status (um round-trip em vez de quatro)
        var contadores = await transacoes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Aprovadas = g.Count(t => t.Status == StatusTransacao.Aprovada),
                Bloqueadas = g.Count(t => t.Status == StatusTransacao.Bloqueada),
                Suspeitas = g.Count(t => t.Status == StatusTransacao.Suspeita),
                AceitasProvisoria = g.Count(t => t.Status == StatusTransacao.AceitaProvisoria),
            })
            .FirstAsync();

        // Aggregações de decimal separadas (compatibilidade SQLite + PostgreSQL)
        var valorTotal = await transacoes.SumAsync(t => t.Valor);
        var valorMedio = await transacoes.AverageAsync(t => t.Valor);

        var temRiskScore = await transacoes.AnyAsync(t => t.RiskScore.HasValue);
        var riskScoreMedio = temRiskScore
            ? await transacoes
                .Where(t => t.RiskScore.HasValue)
                .AverageAsync(t => (double)t.RiskScore!.Value)
            : 0.0;

        return new TransacaoStats
        {
            Total = contadores.Total,
            Aprovadas = contadores.Aprovadas,
            Bloqueadas = contadores.Bloqueadas,
            Suspeitas = contadores.Suspeitas,
            AceitasProvisoria = contadores.AceitasProvisoria,
            ValorTotal = valorTotal,
            ValorMedio = valorMedio,
            RiskScoreMedio = (float)riskScoreMedio,
        };
    }

    public async Task<IReadOnlyList<VolumeHora>> ObterVolumePorHoraAsync()
    {
        var inicio = DateTime.UtcNow.AddHours(-24);

        var volumes = await _context.Transacoes
            .Where(t => t.CreatedAt >= inicio)
            .GroupBy(t => new { t.CreatedAt.Date, t.CreatedAt.Hour })
            .Select(g => new VolumeHora
            {
                Hora = g.Key.Date.AddHours(g.Key.Hour),
                Quantidade = g.Count(),
                Valor = g.Sum(t => t.Valor),
                Bloqueadas = g.Count(t => t.Status == StatusTransacao.Bloqueada),
                Suspeitas = g.Count(t => t.Status == StatusTransacao.Suspeita)
            })
            .OrderBy(v => v.Hora)
            .ToListAsync();

        return volumes;
    }
}
