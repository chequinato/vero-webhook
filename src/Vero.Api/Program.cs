using Microsoft.EntityFrameworkCore;
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

var builder = WebApplication.CreateBuilder(args);

// ────────────────────────────────────────────────────────────────
// Banco de dados (PostgreSQL)
// ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("VeroDb")
    ?? Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
    ?? throw new InvalidOperationException("Connection string não configurada. Defina ConnectionStrings:VeroDb ou VERO_CONNECTION_STRING.");

// ────────────────────────────────────────────────────────────────
// Criptografia em repouso (AES-256)
// ────────────────────────────────────────────────────────────────
var encryptionKey = builder.Configuration["Encryption:Key"]
    ?? Environment.GetEnvironmentVariable("VERO_ENCRYPTION_KEY")
    ?? throw new InvalidOperationException("Chave de criptografia não configurada. Defina Encryption:Key ou VERO_ENCRYPTION_KEY.");

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
// Services
// ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ITransacaoService, TransacaoService>();
builder.Services.AddScoped<AnomalyDetectionService>();

// ────────────────────────────────────────────────────────────────
// Controllers
// ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

// ────────────────────────────────────────────────────────────────
// Pipeline de middlewares (ordem importa!)
// ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();

// 1. Rate Limiting — primeiro, antes de processar qualquer coisa
app.UseCustomRateLimiting();

// 2. HMAC Authentication — valida assinatura do webhook
app.UseHmacAuthentication();

app.MapControllers();

app.Run();
