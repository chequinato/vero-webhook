using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Aws;

/// <summary>
/// Implementação de alertas usando AWS SNS.
/// Substitui o ConsoleAlertaService em produção.
/// </summary>
public class SnsAlertaService : IAlertaService
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger<SnsAlertaService> _logger;
    private readonly string _topicArn;

    public SnsAlertaService(
        IAmazonSimpleNotificationService snsClient,
        ILogger<SnsAlertaService> logger,
        string topicArn)
    {
        _snsClient = snsClient;
        _logger = logger;
        _topicArn = topicArn;
    }

    public async Task EnviarAlertaAsync(string transacaoId, string motivo)
    {
        var request = new PublishRequest
        {
            TopicArn = _topicArn,
            Subject = "Vero Alerta — Anomalia detectada",
            Message = $"Anomalia detectada na transação {transacaoId}.\n" +
                      $"Motivo: {motivo}\n" +
                      $"Timestamp: {DateTime.UtcNow:O}\n\n" +
                      $"Acesse o sistema para mais detalhes."
        };

        var response = await _snsClient.PublishAsync(request);

        _logger.LogWarning(
            "SNS: Alerta enviado para tópico {TopicArn}. " +
            "TransacaoId: {TransacaoId}, Motivo: {Motivo}, MessageId: {MessageId}",
            _topicArn, transacaoId, motivo, response.MessageId);
    }
}
