using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Aws;

/// <summary>
/// Implementação da fila de transações usando AWS SQS.
/// Para uso em produção — substitui o InMemoryFilaTransacao.
///
/// TODO: Implementar quando configurar credenciais AWS e SQS queue real.
/// </summary>
public class SqsFilaTransacao : IFilaTransacao
{
    private readonly ILogger<SqsFilaTransacao> _logger;
    private readonly string _queueUrl;

    public SqsFilaTransacao(ILogger<SqsFilaTransacao> logger, string queueUrl)
    {
        _logger = logger;
        _queueUrl = queueUrl;
    }

    public Task EnviarParaAnaliseAsync(string transacaoId)
    {
        // TODO: Implementar com Amazon.SQS SDK
        // var request = new SendMessageRequest { QueueUrl = _queueUrl, MessageBody = transacaoId };
        // await _sqsClient.SendMessageAsync(request);
        _logger.LogInformation("SQS: Enviaria transação {TransacaoId} para fila {QueueUrl}", transacaoId, _queueUrl);
        return Task.CompletedTask;
    }

    public Task<string?> ConsumirProximaAsync()
    {
        // TODO: Implementar com Amazon.SQS SDK
        // var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest { QueueUrl = _queueUrl, MaxNumberOfMessages = 1 });
        _logger.LogInformation("SQS: Consumiria próxima mensagem da fila {QueueUrl}", _queueUrl);
        return Task.FromResult<string?>(null);
    }
}
