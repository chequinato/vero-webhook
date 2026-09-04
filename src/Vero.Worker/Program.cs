using Microsoft.EntityFrameworkCore;
using Vero.Application.Services;
using Vero.Domain.Interfaces;
using Vero.Domain.Rules;
using Vero.Domain.Rules.Async;
using Vero.Infrastructure.Data;
using Vero.Infrastructure.Notifications;
using Vero.Infrastructure.Queue;
using Vero.Infrastructure.Repositories;
using Vero.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Banco de dados
var connectionString = builder.Configuration.GetConnectionString("VeroDb")
    ?? Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
    ?? "Host=localhost;Database=vero;Username=postgres;Password=postgres";

builder.Services.AddDbContext<VeroDbContext>(options =>
    options.UseNpgsql(connectionString));

// Repositórios
builder.Services.AddScoped<ITransacaoRepository, TransacaoRepository>();
builder.Services.AddScoped<IContaRepository, ContaRepository>();

// Fila e Alertas
builder.Services.AddSingleton<IFilaTransacao, InMemoryFilaTransacao>();
builder.Services.AddSingleton<IAlertaService, ConsoleAlertaService>();

// Regras assíncronas
builder.Services.AddScoped<IRule, VelocityRule>();
builder.Services.AddScoped<IRule, HorarioEstranhoRule>();
builder.Services.AddScoped<IRule, ValorRedondoRule>();

// Services
builder.Services.AddScoped<AnomalyDetectionService>();

// Worker
builder.Services.AddHostedService<AsyncWorker>();

var host = builder.Build();
host.Run();
