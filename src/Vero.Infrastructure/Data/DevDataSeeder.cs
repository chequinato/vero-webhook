using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vero.Domain.Entities;
using Vero.Domain.Enums;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Data;

/// <summary>
/// Popula o banco de dados com dados sintéticos para desenvolvimento e demonstração.
///
/// Cria contas de teste e centenas de transações distribuídas nas últimas 24h
/// com padrões realistas — a maioria aprovadas, algumas suspeitas e bloqueadas.
/// Isso garante que o dashboard já abra com gráficos e estatísticas populados.
/// </summary>
public class DevDataSeeder
{
    private readonly VeroDbContext _context;
    private readonly IRiskScoringService _riskScoringService;
    private readonly ILogger<DevDataSeeder> _logger;
    private readonly Random _random = new(42);

    // Contas de desenvolvimento
    private static readonly (string Numero, string Titular, int Score)[] _contas =
    [
        ("conta_123", "Maria Silva", 85),
        ("conta_456", "João Santos", 72),
        ("conta_789", "Ana Oliveira", 25),
        ("conta_000", "Carlos Ferreira", 50),
        ("conta_111", "Beatriz Costa", 92),
        ("conta_222", "Pedro Almeida", 15),
    ];

    public DevDataSeeder(
        VeroDbContext context,
        IRiskScoringService riskScoringService,
        ILogger<DevDataSeeder> logger)
    {
        _context = context;
        _riskScoringService = riskScoringService;
        _logger = logger;
    }

    public async Task SeedAsync(int totalTransacoes = 300)
    {
        // Criar contas se não existem
        await SeedContasAsync();

        // Só semear transações se o banco estiver vazio
        if (await _context.Transacoes.AnyAsync())
        {
            _logger.LogInformation("Banco já contém transações, pulando seed.");
            return;
        }

        _logger.LogInformation("Semeando {Total} transações sintéticas...", totalTransacoes);

        var contas = await _context.Contas.ToListAsync();
        var agora = DateTime.UtcNow;

        for (int i = 0; i < totalTransacoes; i++)
        {
            var transacao = GerarTransacaoSintetica(contas, agora, i);

            // Calcular risk score via ML
            var remetente = contas.First(c => c.Id == transacao.RemetenteId);
            var risco = await _riskScoringService.CalcularRiscoAsync(transacao, remetente);
            transacao.RiskScore = risco;

            // Decidir status baseado nas regras e no risk score
            DefinirStatus(transacao, remetente, risco);

            _context.Transacoes.Add(transacao);

            // Registrar no histórico
            _context.TransacaoHistoricoStatus.Add(new TransacaoHistoricoStatus
            {
                TransacaoId = transacao.Id,
                StatusAnterior = null,
                StatusNovo = transacao.Status.ToString(),
                Motivo = transacao.Motivo,
                AlteradoEm = transacao.CreatedAt
            });
        }

        await _context.SaveChangesAsync();

        var stats = await _context.Transacoes.GroupBy(_ => 1).Select(g => new
        {
            Total = g.Count(),
            Aprovadas = g.Count(t => t.Status == StatusTransacao.Aprovada),
            Bloqueadas = g.Count(t => t.Status == StatusTransacao.Bloqueada),
            Suspeitas = g.Count(t => t.Status == StatusTransacao.Suspeita),
        }).FirstAsync();

        _logger.LogInformation(
            "Seed concluído: {Total} transações ({Aprovadas} aprovadas, {Bloqueadas} bloqueadas, {Suspeitas} suspeitas)",
            stats.Total, stats.Aprovadas, stats.Bloqueadas, stats.Suspeitas);
    }

    private async Task SeedContasAsync()
    {
        foreach (var (numero, titular, score) in _contas)
        {
            if (!await _context.Contas.AnyAsync(c => c.NumeroConta == numero))
            {
                _context.Contas.Add(new Conta
                {
                    NumeroConta = numero,
                    Titular = titular,
                    Score = score,
                    CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    private Transacao GerarTransacaoSintetica(List<Conta> contas, DateTime agora, int index)
    {
        // Distribuir transações nas últimas 24h com mais concentração em horários comerciais
        var horasAtras = _random.NextDouble() * 24;
        var timestamp = agora.AddHours(-horasAtras);

        // Maior chance de horários comerciais (8h-20h)
        if (_random.NextDouble() < 0.7)
        {
            var hora = _random.Next(8, 20);
            timestamp = timestamp.Date.AddHours(hora).AddMinutes(_random.Next(0, 60));
            if (timestamp > agora) timestamp = agora.AddMinutes(-_random.Next(1, 60));
        }

        // Selecionar remetente e destinatário diferentes
        var remetenteIdx = _random.Next(contas.Count);
        var destinatarioIdx = (remetenteIdx + 1 + _random.Next(contas.Count - 1)) % contas.Count;

        // 90% das transações são valores normais, 10% são padrões suspeitos
        decimal valor;
        if (_random.NextDouble() < 0.90)
        {
            // Valores normais: maioria entre R$50 e R$5.000
            valor = (decimal)(_random.NextDouble() * 4950 + 50);
        }
        else
        {
            // Valores suspeitos: altos, redondos ou fora do padrão
            var padrao = _random.Next(0, 3);
            valor = padrao switch
            {
                0 => (decimal)(_random.NextDouble() * 400_000 + 100_001), // > 100k
                1 => _random.Next(1, 50) * 1000m, // Valor redondo
                _ => (decimal)(_random.NextDouble() * 50_000 + 10_000), // 10k-60k
            };
        }

        return new Transacao
        {
            Id = $"txn_seed_{index:D4}",
            RemetenteId = contas[remetenteIdx].Id,
            DestinatarioId = contas[destinatarioIdx].Id,
            Valor = Math.Round(valor, 2),
            Tipo = "pix",
            Moeda = "BRL",
            Timestamp = timestamp,
            CreatedAt = timestamp,
        };
    }

    private void DefinirStatus(Transacao transacao, Conta remetente, float riskScore)
    {
        // Regras síncronas (bloqueio imediato)
        if (transacao.Valor > 100_000m)
        {
            transacao.Status = StatusTransacao.Bloqueada;
            transacao.Motivo = "valor_alto";
            return;
        }

        if (remetente.Score < 35)
        {
            transacao.Status = StatusTransacao.Bloqueada;
            transacao.Motivo = "score_baixo";
            return;
        }

        // Baseado no risk score do ML
        if (riskScore > 0.7f)
        {
            transacao.Status = StatusTransacao.Suspeita;
            transacao.Motivo = "risco_ml_alto";
            return;
        }

        if (riskScore > 0.4f && _random.NextDouble() < 0.3)
        {
            transacao.Status = StatusTransacao.Suspeita;
            transacao.Motivo = transacao.Timestamp.Hour is >= 1 and <= 5
                ? "horario_estranho"
                : "velocity_alta";
            return;
        }

        transacao.Status = StatusTransacao.Aprovada;
        transacao.Motivo = null;
    }
}
