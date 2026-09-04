using Microsoft.EntityFrameworkCore;
using Vero.Domain.Entities;
using Vero.Domain.Interfaces;
using Vero.Infrastructure.Data;

namespace Vero.Infrastructure.Repositories;

public class ContaRepository : IContaRepository
{
    private readonly VeroDbContext _context;

    public ContaRepository(VeroDbContext context)
    {
        _context = context;
    }

    public async Task<Conta?> ObterPorIdAsync(int id)
    {
        return await _context.Contas.FindAsync(id);
    }

    public async Task<Conta?> ObterPorNumeroContaAsync(string numeroConta)
    {
        return await _context.Contas
            .FirstOrDefaultAsync(c => c.NumeroConta == numeroConta);
    }

    public async Task AdicionarAsync(Conta conta)
    {
        await _context.Contas.AddAsync(conta);
        await _context.SaveChangesAsync();
    }
}
