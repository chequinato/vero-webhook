using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vero.Domain.Interfaces;
using Vero.ML.Services;

namespace Vero.ML;

/// <summary>
/// Registro do modelo de risco. O resto do sistema só conhece
/// <see cref="IRiskScoringService"/> — trocar ML.NET por outra coisa
/// (uma API externa, outro algoritmo) não afeta nenhum service.
/// </summary>
public static class DependencyInjection
{
    public static string CaminhoModeloPadrao =>
        Path.Combine(AppContext.BaseDirectory, "Models", "fraud_model.zip");

    public static IServiceCollection AddVeroMl(this IServiceCollection services, string? caminhoModelo = null)
    {
        var caminho = caminhoModelo ?? CaminhoModeloPadrao;

        services.AddSingleton<IRiskScoringService>(sp =>
            new MlRiskScoringService(caminho, sp.GetRequiredService<ILogger<MlRiskScoringService>>()));

        return services;
    }
}
