using Vero.Application.Models;

namespace Vero.Application.Interfaces;

/// <summary>
/// Pede ao modelo de ML uma nova decisão sobre uma transação em revisão.
/// </summary>
public interface IReavaliacaoService
{
    Task<ResultadoReavaliacao> ReavaliarAsync(string transacaoId);
}
