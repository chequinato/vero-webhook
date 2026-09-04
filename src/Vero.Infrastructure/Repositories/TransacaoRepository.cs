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
}
