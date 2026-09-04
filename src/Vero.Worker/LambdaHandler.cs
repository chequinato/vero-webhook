using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vero.Application.Services;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Async;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Notifications;
using Vero.Infrastructure.Repositories;

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

        // Banco de dados
        var connectionString = Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
            ?? "Host=localhost;Database=vero;Username=postgres;Password=postgres";

        services.AddDbContext<VeroDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositórios
        services.AddScoped<ITransacaoRepository, TransacaoRepository>();
        services.AddScoped<IContaRepository, ContaRepository>();

        // Alertas (console em Lambda — o log vai pro CloudWatch)
        services.AddSingleton<IAlertaService, ConsoleAlertaService>();

        // Regras assíncronas
        services.AddScoped<IRule, VelocityRule>();
        services.AddScoped<IRule, HorarioEstranhoRule>();
        services.AddScoped<IRule, ValorRedondoRule>();

        // Services
        services.AddScoped<AnomalyDetectionService>();

        // Logging
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
