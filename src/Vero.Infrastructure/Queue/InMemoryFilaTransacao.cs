using System.Collections.Concurrent;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Queue;

/// <summary>
/// Implementação em memória da fila de transações para desenvolvimento local.
/// Em produção, será substituída pela implementação com SQS.
/// </summary>
public class InMemoryFilaTransacao : IFilaTransacao
{
    private readonly ConcurrentQueue<string> _fila = new();

    public Task EnviarParaAnaliseAsync(string transacaoId)
    {
        _fila.Enqueue(transacaoId);
        return Task.CompletedTask;
    }

    public Task<string?> ConsumirProximaAsync()
    {
        return Task.FromResult(_fila.TryDequeue(out var id) ? id : null);
    }
}
