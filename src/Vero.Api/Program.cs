using Microsoft.EntityFrameworkCore;
using Vero.Application.Services;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Sync;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Notifications;
using Vero.Infrastructure.Queue;
using Vero.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ────────────────────────────────────────────────────────────────
// Banco de dados (PostgreSQL)
// ────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("VeroDb")
    ?? Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
    ?? "Host=localhost;Database=vero;Username=postgres;Password=postgres";

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
// Pipeline de middlewares
// ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
