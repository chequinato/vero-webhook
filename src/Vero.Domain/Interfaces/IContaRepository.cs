using Vero.Domain.Entities;

namespace Vero.Domain.Interfaces;

public interface IContaRepository
{
    Task<Conta?> ObterPorIdAsync(int id);
    Task<Conta?> ObterPorNumeroContaAsync(string numeroConta);
    Task AdicionarAsync(Conta conta);
}
