using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vero.Domain.Interfaces;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Notifications;
using Vero.Infrastructure.Queue;
using Vero.Infrastructure.Repositories;
using Vero.Infrastructure.Security;

namespace Vero.Infrastructure;

/// <summary>
/// Registro de dependências da camada Infrastructure.
///
/// É aqui que cada interface do Domain ganha sua implementação concreta
/// (Adapter Pattern): ITransacaoRepository → EF Core, IFilaTransacao → fila
/// em memória, IAlertaService → log no console. Para ir para produção com
/// SQS/SNS, só este arquivo muda — services e regras continuam iguais.
/// </summary>
public static class DependencyInjection
{
    private const string SqliteEmMemoria = "DataSource=VeroDev;Mode=Memory;Cache=Shared";

    /// <summary>
    /// PostgreSQL + criptografia em repouso. Usado em produção e pelo Worker.
    /// Valores nulos caem nas variáveis de ambiente VERO_CONNECTION_STRING e
    /// VERO_ENCRYPTION_KEY.
    ///
    /// Não existe valor padrão no código de propósito: se a configuração
    /// faltar, a aplicação não sobe. Um fallback silencioso faria a produção
    /// rodar com a senha e a chave de dev, que estão públicas no repositório.
    /// </summary>
    public static IServiceCollection AddVeroInfrastructure(
        this IServiceCollection services,
        string? connectionString = null,
        string? chaveCriptografia = null)
    {
        connectionString ??= Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
            ?? throw ConfiguracaoAusente("ConnectionStrings:VeroDb", "VERO_CONNECTION_STRING");
        chaveCriptografia ??= Environment.GetEnvironmentVariable("VERO_ENCRYPTION_KEY")
            ?? throw ConfiguracaoAusente("Encryption:Key", "VERO_ENCRYPTION_KEY");

        services.AddDbContext<VeroDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<ICryptoService>(new AesCryptoService(chaveCriptografia));

        return services.AddServicosComuns();
    }

    /// <summary>
    /// SQLite em memória, sem criptografia: zero dependência para rodar local.
    /// O banco vive enquanto o processo estiver de pé.
    /// </summary>
    public static IServiceCollection AddVeroInfrastructureEmMemoria(this IServiceCollection services)
    {
        services.AddDbContext<VeroDbContext>(options => options.UseSqlite(SqliteEmMemoria));

        // Banco em memória some quando a última conexão fecha — esta fica aberta
        var keepAlive = new SqliteConnection(SqliteEmMemoria);
        keepAlive.Open();
        services.AddSingleton(keepAlive);

        services.AddScoped<DevDataSeeder>();

        return services.AddServicosComuns();
    }

    private static InvalidOperationException ConfiguracaoAusente(string chaveConfig, string variavelAmbiente) =>
        new($"Configuração obrigatória ausente: defina '{chaveConfig}' no appsettings " +
            $"ou a variável de ambiente {variavelAmbiente}. Veja o .env.example.");

    private static IServiceCollection AddServicosComuns(this IServiceCollection services)
    {
        services.AddScoped<IContaRepository, ContaRepository>();
        services.AddScoped<ITransacaoRepository, TransacaoRepository>();

        // Implementações locais; em produção troque por SqsFilaTransacao / SnsAlertaService
        services.AddSingleton<IFilaTransacao, InMemoryFilaTransacao>();
        services.AddSingleton<IAlertaService, ConsoleAlertaService>();

        return services;
    }
}
