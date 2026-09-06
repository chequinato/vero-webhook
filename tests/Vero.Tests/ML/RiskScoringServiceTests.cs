using Microsoft.Extensions.Logging.Abstractions;
using Vero.Domain.Entities;
using Vero.ML.Services;
using Vero.ML.Training;

namespace Vero.Tests.ML;

public class RiskScoringServiceTests : IDisposable
{
    private readonly string _tempModelPath;
    private readonly MlRiskScoringService _service;

    public RiskScoringServiceTests()
    {
        _tempModelPath = Path.Combine(Path.GetTempPath(), $"vero_test_model_{Guid.NewGuid()}.zip");

        // Treinar modelo para os testes
        var trainer = new ModelTrainer();
        trainer.TreinarESalvar(_tempModelPath, totalAmostras: 2000);

        _service = new MlRiskScoringService(
            _tempModelPath,
            NullLogger<MlRiskScoringService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (File.Exists(_tempModelPath))
            File.Delete(_tempModelPath);
    }

    [Fact]
    public async Task CalcularRisco_TransacaoNormal_DeveRetornarRiscoBaixo()
    {
        var transacao = new Transacao
        {
            Id = "txn_normal",
            Valor = 500m,
            Timestamp = DateTime.UtcNow.Date.AddHours(14), // 14h — horário normal
        };
        var conta = new Conta { Score = 90 };

        var risco = await _service.CalcularRiscoAsync(transacao, conta);

        Assert.InRange(risco, 0f, 1f);
        // Transação normal com score alto deve ter risco relativamente baixo
        Assert.True(risco < 0.7f,
            $"Risco de transação normal deveria ser < 0.7, obtido: {risco:F4}");
    }

    [Fact]
    public async Task CalcularRisco_TransacaoSuspeita_DeveRetornarRiscoAlto()
    {
        var transacao = new Transacao
        {
            Id = "txn_suspeita",
            Valor = 150_000m, // Valor muito alto
            Timestamp = DateTime.UtcNow.Date.AddHours(3), // 3h da manhã
        };
        var conta = new Conta { Score = 15 }; // Score muito baixo

        var risco = await _service.CalcularRiscoAsync(transacao, conta);

        Assert.InRange(risco, 0f, 1f);
        // Transação com múltiplos sinais de fraude deve ter risco alto
        Assert.True(risco > 0.3f,
            $"Risco de transação suspeita deveria ser > 0.3, obtido: {risco:F4}");
    }

    [Fact]
    public async Task CalcularRisco_SempreRetornaProbabilidadeValida()
    {
        var cenarios = new[]
        {
            (valor: 100m, score: 50, hora: 10),
            (valor: 999_999m, score: 1, hora: 3),
            (valor: 1m, score: 100, hora: 12),
            (valor: 50_000m, score: 25, hora: 2),
        };

        foreach (var (valor, score, hora) in cenarios)
        {
            var transacao = new Transacao
            {
                Id = $"txn_{valor}",
                Valor = valor,
                Timestamp = DateTime.UtcNow.Date.AddHours(hora),
            };
            var conta = new Conta { Score = score };

            var risco = await _service.CalcularRiscoAsync(transacao, conta);

            Assert.InRange(risco, 0f, 1f);
        }
    }

    [Fact]
    public async Task CalcularRisco_ContaNula_DeveUsarScorePadrao()
    {
        var transacao = new Transacao
        {
            Id = "txn_sem_conta",
            Valor = 500m,
            Timestamp = DateTime.UtcNow.Date.AddHours(14),
        };

        // Não deve lançar exceção mesmo sem conta
        var risco = await _service.CalcularRiscoAsync(transacao, remetente: null);

        Assert.InRange(risco, 0f, 1f);
    }

    [Fact]
    public void ExtrairFeatures_DeveMapearCorretamente()
    {
        var transacao = new Transacao
        {
            Id = "txn_test",
            Valor = 5000m,
            Timestamp = new DateTime(2024, 6, 15, 3, 30, 0, DateTimeKind.Utc), // 3h
        };
        var conta = new Conta { Score = 40 };

        var features = MlRiskScoringService.ExtrairFeatures(transacao, conta);

        Assert.Equal(5000f, features.Valor);
        Assert.Equal(3f, features.HoraDoDia);
        Assert.Equal(40f, features.ScoreRemetente);
        Assert.Equal(1f, features.IsValorRedondo); // 5000 é múltiplo de 1000
        Assert.Equal(1f, features.IsHorarioEstranho); // 3h está entre 1-5
        Assert.Equal(5000f / 40f, features.RazaoValorScore);
    }
}
