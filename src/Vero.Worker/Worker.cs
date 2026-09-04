using Vero.Application.Services;
using Vero.Domain.Interfaces;

namespace Vero.Worker;

/// <summary>
/// Worker que consome a fila de transações e roda a análise assíncrona.
/// </summary>
public class AsyncWorker(
    ILogger<AsyncWorker> logger,
    IServiceScopeFactory scopeFactory,
    IFilaTransacao filaTransacao) : BackgroundService
{
    /// <summary>
    /// Intervalo entre verificações da fila quando está vazia.
    /// </summary>
    private static readonly TimeSpan IntervaloPolling = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de análise assíncrona iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var transacaoId = await filaTransacao.ConsumirProximaAsync();

                if (transacaoId is not null)
                {
                    logger.LogInformation("Processando análise assíncrona da transação {TransacaoId}", transacaoId);

                    using var scope = scopeFactory.CreateScope();
                    var anomalyService = scope.ServiceProvider.GetRequiredService<AnomalyDetectionService>();
                    await anomalyService.AnalisarAsync(transacaoId);

                    logger.LogInformation("Análise da transação {TransacaoId} concluída.", transacaoId);
                }
                else
                {
                    await Task.Delay(IntervaloPolling, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar mensagem da fila.");
                await Task.Delay(IntervaloPolling, stoppingToken);
            }
        }

        logger.LogInformation("Worker de análise assíncrona encerrado.");
    }
}
