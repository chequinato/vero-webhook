using Microsoft.EntityFrameworkCore;
using Vero.Api.Hubs;
using Vero.Api.Middleware;
using Vero.Application.Services;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Sync;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Notifications;
using Vero.Infrastructure.Queue;
using Vero.Infrastructure.Repositories;
using Vero.Infrastructure.Security;
using Vero.ML.Services;

var builder = WebApplication.CreateBuilder(args);

// ────────────────────────────────────────────────────────────────
// Banco de dados (PostgreSQL)
// ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("VeroDb")
    ?? Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
    ?? "Host=localhost;Database=vero;Username=postgres;Password=postgres";

// ────────────────────────────────────────────────────────────────
// Criptografia em repouso (AES-256)
// ────────────────────────────────────────────────────────────────
var encryptionKey = builder.Configuration["Encryption:Key"]
    ?? Environment.GetEnvironmentVariable("VERO_ENCRYPTION_KEY")
    ?? "vero-dev-key-nao-usar-em-producao";

var cryptoService = new AesCryptoService(encryptionKey);
builder.Services.AddSingleton<ICryptoService>(cryptoService);

builder.Services.AddDbContext<VeroDbContext>(options =>
    options.UseNpgsql(connectionString));

// ────────────────────────────────────────────────────────────────
// Repositórios
// ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IContaRepository, ContaRepository>();
builder.Services.AddScoped<ITransacaoRepository, TransacaoRepository>();

// ────────────────────────────────────────────────────────────────
// Fila e Alertas (implementações locais; trocar por SQS/SNS em prod)
// ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IFilaTransacao, InMemoryFilaTransacao>();
builder.Services.AddSingleton<IAlertaService, ConsoleAlertaService>();

// ────────────────────────────────────────────────────────────────
// Regras síncronas (injetadas como IEnumerable<IRule> no TransacaoService)
// ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRule, ValorAltoRule>();
builder.Services.AddScoped<IRule, ScoreBaixoRule>();

// ────────────────────────────────────────────────────────────────
// Machine Learning — Risk Scoring
// ────────────────────────────────────────────────────────────────
var modelPath = builder.Configuration["ML:ModelPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "Models", "fraud_model.zip");

builder.Services.AddSingleton<IRiskScoringService>(sp =>
    new MlRiskScoringService(modelPath, sp.GetRequiredService<ILogger<MlRiskScoringService>>()));

// ────────────────────────────────────────────────────────────────
// Services
// ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddScoped<AnomalyDetectionService>();

// ────────────────────────────────────────────────────────────────
// SignalR (real-time para o dashboard)
// ────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<TransactionHubNotifier>();

// ────────────────────────────────────────────────────────────────
// CORS (permitir React dev server)
// ────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // Vite dev server
                "http://localhost:3000")  // Alternativa
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Necessário para SignalR
    });
});

// ────────────────────────────────────────────────────────────────
// Controllers
// ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

// ────────────────────────────────────────────────────────────────
// Pipeline de middlewares (ordem importa!)
// ────────────────────────────────────────────────────────────────
app.UseCors("DashboardPolicy");

app.UseHttpsRedirection();

// 1. Rate Limiting — primeiro, antes de processar qualquer coisa
app.UseCustomRateLimiting();

// 2. HMAC Authentication — valida assinatura do webhook
app.UseHmacAuthentication();

app.MapControllers();

// 3. SignalR Hub — endpoint para o dashboard
app.MapHub<TransactionHub>("/hubs/transactions");

app.Run();
