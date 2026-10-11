using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vero.Application;
using Vero.Application.Services;
using Vero.Infrastructure;

namespace Vero.Worker;

/// <summary>
/// Handler Lambda para processamento de transações vindas da fila SQS.
/// Alternativa ao AsyncWorker (BackgroundService) — roda na AWS Lambda (Always Free).
///
/// Fluxo: SQS → Lambda (este handler) → analisa transação → atualiza status → SNS (se anomalia)
/// </summary>
public class LambdaHandler
{
    private readonly IServiceProvider _serviceProvider;

    public LambdaHandler()
    {
        var services = new ServiceCollection();

        // Mesmas camadas da Api e do Worker, registradas pelos mesmos métodos.
        // Connection string e chave vêm das variáveis de ambiente da Lambda.
        services.AddVeroInfrastructure();
        services.AddVeroAnaliseAssincrona();

        // Logging (vai pro CloudWatch)
        services.AddLogging(builder => builder.AddConsole());

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Entry point da Lambda — chamado pelo trigger SQS.
    /// Cada mensagem no batch contém o ID de uma transação para analisar.
    /// </summary>
    public async Task HandleAsync(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogInformation(
            $"Lambda invocada com {sqsEvent.Records.Count} mensagem(ns) da fila.");

        foreach (var record in sqsEvent.Records)
        {
            var transacaoId = record.Body;
            context.Logger.LogInformation($"Processando transação: {transacaoId}");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var anomalyService = scope.ServiceProvider
                    .GetRequiredService<AnomalyDetectionService>();

                await anomalyService.AnalisarAsync(transacaoId);

                context.Logger.LogInformation($"Transação {transacaoId} analisada com sucesso.");
            }
            catch (Exception ex)
            {
                context.Logger.LogError(
                    $"Erro ao processar transação {transacaoId}: {ex.Message}");
                throw; // Re-throw para que o SQS re-enfileire (DLQ após 3 falhas)
            }
        }
    }
}
