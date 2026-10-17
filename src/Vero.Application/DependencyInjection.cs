using Microsoft.Extensions.DependencyInjection;
using Vero.Application.Interfaces;
using Vero.Application.Services;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Async;
using Vero.Domain.Rules.Sync;

namespace Vero.Application;

/// <summary>
/// Registro de dependências da camada Application (e das regras do Domain).
///
/// Cada camada sabe registrar as próprias peças; quem monta a aplicação
/// (Api, Worker, Lambda) só chama um dos métodos Add* abaixo.
/// Antes, Api/Program.cs, Worker/Program.cs e LambdaHandler.cs repetiam essas
/// linhas — e já tinham divergido entre si.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Tudo o que a Api precisa: recebimento, consulta, reavaliação e
    /// também a análise assíncrona.
    /// </summary>
    public static IServiceCollection AddVeroApplication(this IServiceCollection services)
    {
        // Regras síncronas — avaliadas no recebimento do webhook.
        // Para criar uma regra nova: crie a classe e adicione uma linha aqui.
        services.AddScoped<IRegraSincrona, ValorAltoRule>();
        services.AddScoped<IRegraSincrona, ScoreBaixoRule>();

        services.AddScoped<CalculadoraDeRisco>();
        services.AddScoped<ITransacaoService, TransacaoService>();
        services.AddScoped<IReavaliacaoService, ReavaliacaoService>();

        return services.AddVeroAnaliseAssincrona();
    }

    /// <summary>
    /// Só a análise em background — é o que o Worker e a Lambda usam.
    /// Eles não registram o resto porque não têm ML nem SignalR, e o
    /// container de DI recusa (em Development) services que não consegue montar.
    /// </summary>
    public static IServiceCollection AddVeroAnaliseAssincrona(this IServiceCollection services)
    {
        services.AddScoped<IRegraAssincrona, VelocityRule>();
        services.AddScoped<IRegraAssincrona, HorarioEstranhoRule>();
        services.AddScoped<IRegraAssincrona, ValorRedondoRule>();

        services.AddScoped<AnomalyDetectionService>();

        return services;
    }
}
