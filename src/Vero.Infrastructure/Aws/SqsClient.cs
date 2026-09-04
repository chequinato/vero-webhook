using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Aws;

/// <summary>
/// Implementação da fila de transações usando AWS SQS.
/// Substitui o InMemoryFilaTransacao em produção.
/// </summary>
public class SqsFilaTransacao : IFilaTransacao
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsFilaTransacao> _logger;
    private readonly string _queueUrl;

    public SqsFilaTransacao(IAmazonSQS sqsClient, ILogger<SqsFilaTransacao> logger, string queueUrl)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _queueUrl = queueUrl;
    }

    public async Task EnviarParaAnaliseAsync(string transacaoId)
    {
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = transacaoId
        };

        var response = await _sqsClient.SendMessageAsync(request);

        _logger.LogInformation(
            "SQS: Transação {TransacaoId} enviada para fila. MessageId: {MessageId}",
            transacaoId, response.MessageId);
    }

    public async Task<string?> ConsumirProximaAsync()
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 20 // Long polling
        };

        var response = await _sqsClient.ReceiveMessageAsync(request);

        if (response.Messages.Count == 0)
            return null;

        var message = response.Messages[0];

        // Deleta a mensagem após consumir (ack)
        await _sqsClient.DeleteMessageAsync(_queueUrl, message.ReceiptHandle);

        _logger.LogInformation(
            "SQS: Mensagem consumida da fila. TransacaoId: {TransacaoId}",
            message.Body);

        return message.Body;
    }
}
