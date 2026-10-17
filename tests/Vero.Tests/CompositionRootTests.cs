using Microsoft.Extensions.DependencyInjection;
using Moq;
using Vero.Application;
using Vero.Application.Interfaces;
using Vero.Application.Services;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Async;
using Vero.Domain.Rules.Sync;
using Vero.Infrastructure;

namespace Vero.Tests;

/// <summary>
/// Garante que os métodos Add* de cada camada montam um container completo:
/// se alguém criar um service novo e esquecer de registrar uma dependência,
/// estes testes quebram aqui — e não em produção.
/// </summary>
public class CompositionRootTests
{
    private static readonly ServiceProviderOptions Validar = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    };

    [Fact]
    public void Api_DeveMontarTodosOsServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeroInfrastructureEmMemoria();
        services.AddVeroApplication();

        // Peças de fora da Application entram como dublês: o modelo de ML
        // treinaria do zero se não achasse o arquivo, e o notificador real
        // depende do SignalR da Api.
        services.AddSingleton(Mock.Of<IRiskScoringService>());
        services.AddSingleton(Mock.Of<INotificadorTransacao>());

        using var provider = services.BuildServiceProvider(Validar);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITransacaoService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReavaliacaoService>());
    }

    [Fact]
    public void Worker_DeveMontarAnaliseAssincronaSemMlNemSignalR()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeroInfrastructure("Host=localhost;Database=teste", "chave-de-teste");
        services.AddVeroAnaliseAssincrona();

        using var provider = services.BuildServiceProvider(Validar);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AnomalyDetectionService>());
    }

    [Fact]
    public void Infrastructure_SemConfiguracao_DeveFalharEmVezDeUsarSenhaPadrao()
    {
        // Se a máquina tiver a variável definida, o fallback dela é legítimo
        if (Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING") is not null) return;

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddVeroInfrastructure());

        Assert.Contains("VERO_CONNECTION_STRING", ex.Message);
    }

    [Fact]
    public void Regras_CadaServiceRecebeSoAsRegrasDoSeuTipo()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeroInfrastructureEmMemoria();
        services.AddVeroApplication();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var sincronas = scope.ServiceProvider.GetServices<IRegraSincrona>().Select(r => r.GetType()).ToList();
        var assincronas = scope.ServiceProvider.GetServices<IRegraAssincrona>().Select(r => r.GetType()).ToList();

        Assert.Equal([typeof(ValorAltoRule), typeof(ScoreBaixoRule)], sincronas);
        Assert.Equal([typeof(VelocityRule), typeof(HorarioEstranhoRule), typeof(ValorRedondoRule)], assincronas);
    }
}
