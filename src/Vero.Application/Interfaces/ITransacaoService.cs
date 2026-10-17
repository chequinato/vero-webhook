using Vero.Application.Models;
using Vero.Domain.Entities;

namespace Vero.Application.Interfaces;

/// <summary>
/// Casos de uso de transação expostos para a camada de entrega (Api).
///
/// Este contrato morava no Domain. Mas o Domain não deveria saber que existem
/// "serviços de aplicação": ele só guarda entidades, regras e as interfaces
/// que o próprio negócio precisa (repositórios, fila, alerta). Contratos que
/// a Api consome ficam aqui na Application.
/// </summary>
public interface ITransacaoService
{
    /// <summary>
    /// Recebe uma transação do webhook: resolve as contas, calcula o score de
    /// risco, aplica as regras síncronas, persiste, enfileira para análise
    /// assíncrona e notifica o dashboard.
    /// </summary>
    Task<ResultadoRecebimento> ProcessarTransacaoAsync(NovaTransacao novaTransacao);

    Task<Transacao?> ConsultarStatusAsync(string transacaoId);

    /// <summary>
    /// Lista todas as transações com status suspeita ou bloqueada.
    /// </summary>
    Task<IReadOnlyList<Transacao>> ListarSuspeitasAsync();
}
