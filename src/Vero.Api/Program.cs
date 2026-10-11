using Vero.Api.Hubs;
using Vero.Api.Middleware;
using Vero.Application;
using Vero.Domain.Interfaces;
using Vero.Infrastructure;
using Vero.Infrastructure.Data;
using Vero.ML;

// ────────────────────────────────────────────────────────────────
// Composition Root
//
// Este é o ÚNICO lugar que conhece todas as camadas ao mesmo tempo.
// Cada camada expõe um método Add* que registra as próprias peças;
// aqui a gente só escolhe quais peças montar.
// ────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// --dev usa SQLite em memória (zero dependência)
var usarSqlite = args.Contains("--dev")
    || builder.Configuration.GetValue<bool>("UseSqlite")
    || Environment.GetEnvironmentVariable("VERO_USE_SQLITE") == "true";

// ── Infrastructure: banco, repositórios, fila, alertas ──────────
if (usarSqlite)
{
    builder.Services.AddVeroInfrastructureEmMemoria();
    builder.Logging.AddConsole();
    Console.WriteLine("🔧 Modo DEV: usando SQLite em memória (sem PostgreSQL)");
}
else
{
    builder.Services.AddVeroInfrastructure(
        builder.Configuration.GetConnectionString("VeroDb"),
        builder.Configuration["Encryption:Key"]);
}

// ── ML, Application (services + regras) ─────────────────────────
builder.Services.AddVeroMl(builder.Configuration["ML:ModelPath"]);
builder.Services.AddVeroApplication();

// ── Api: tempo real, CORS, controllers ──────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificadorTransacao, TransactionHubNotifier>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite dev server
                "http://localhost:5174",  // Vite fallback port
                "http://localhost:3000")  // Alternativa
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Necessário para SignalR
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// ── Modo dev: cria o schema e popula dados de exemplo ───────────
if (usarSqlite)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeroDbContext>();

    // Criar schema sem migrations (SQLite InMemory não persiste migrations)
    await db.Database.EnsureCreatedAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DevDataSeeder>();
    await seeder.SeedAsync(totalTransacoes: 300);
}

// ────────────────────────────────────────────────────────────────
// Pipeline de middlewares (ordem importa! — Chain of Responsibility)
// ────────────────────────────────────────────────────────────────
app.UseCors("DashboardPolicy");

// Não redirecionar para HTTPS em modo dev
if (!usarSqlite)
{
    app.UseHttpsRedirection();
}

// 1. Rate Limiting — primeiro, antes de processar qualquer coisa
app.UseCustomRateLimiting();

// 2. HMAC Authentication — valida assinatura do webhook
app.UseHmacAuthentication();

app.MapControllers();

// 3. SignalR Hub — endpoint para o dashboard
app.MapHub<TransactionHub>("/hubs/transactions");

app.Run();
