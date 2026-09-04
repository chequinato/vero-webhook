using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Notifications;

/// <summary>
/// Implementação de alertas via console/log para desenvolvimento local.
/// Em produção, será substituída pela implementação com SNS.
/// </summary>
public class ConsoleAlertaService : IAlertaService
{
    private readonly ILogger<ConsoleAlertaService> _logger;

    public ConsoleAlertaService(ILogger<ConsoleAlertaService> logger)
    {
        _logger = logger;
    }

    public Task EnviarAlertaAsync(string transacaoId, string motivo)
    {
        _logger.LogWarning(
            "🚨 ALERTA: Anomalia detectada na transação {TransacaoId} — motivo: {Motivo}",
            transacaoId, motivo);
        return Task.CompletedTask;
    }
}
