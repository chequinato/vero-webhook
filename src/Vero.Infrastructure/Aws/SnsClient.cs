using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Aws;

/// <summary>
/// Implementação de alertas usando AWS SNS.
/// Para uso em produção — substitui o ConsoleAlertaService.
///
/// TODO: Implementar quando configurar credenciais AWS e SNS topic real.
/// </summary>
public class SnsAlertaService : IAlertaService
{
    private readonly ILogger<SnsAlertaService> _logger;
    private readonly string _topicArn;

    public SnsAlertaService(ILogger<SnsAlertaService> logger, string topicArn)
    {
        _logger = logger;
        _topicArn = topicArn;
    }

    public Task EnviarAlertaAsync(string transacaoId, string motivo)
    {
        // TODO: Implementar com Amazon.SimpleNotificationService SDK
        // var request = new PublishRequest { TopicArn = _topicArn, Subject = "Alerta Vero", Message = $"..." };
        // await _snsClient.PublishAsync(request);
        _logger.LogWarning(
            "SNS: Enviaria alerta para tópico {TopicArn} — transação {TransacaoId}, motivo: {Motivo}",
            _topicArn, transacaoId, motivo);
        return Task.CompletedTask;
    }
}
