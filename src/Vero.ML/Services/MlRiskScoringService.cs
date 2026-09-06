using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Vero.Domain.Entities;
using Vero.Domain.Interfaces;
using Vero.ML.Models;
using Vero.ML.Training;

namespace Vero.ML.Services;

/// <summary>
/// Implementação do serviço de scoring de risco usando ML.NET.
///
/// Carrega o modelo treinado e usa o PredictionEngine para calcular
/// a probabilidade de fraude de cada transação em tempo real.
///
/// Se o modelo não existir no path configurado, treina automaticamente
/// com dados sintéticos na inicialização.
/// </summary>
public class MlRiskScoringService : IRiskScoringService, IDisposable
{
    private readonly PredictionEngine<TransacaoFeatures, TransacaoPrediction> _predictionEngine;
    private readonly ILogger<MlRiskScoringService> _logger;

    public MlRiskScoringService(string modelPath, ILogger<MlRiskScoringService> logger)
    {
        _logger = logger;

        // Se o modelo não existe, treinar na inicialização
        if (!File.Exists(modelPath))
        {
            _logger.LogWarning("Modelo ML não encontrado em {Path}. Treinando com dados sintéticos...", modelPath);

            var trainer = new ModelTrainer();
            var metrics = trainer.TreinarESalvar(modelPath, totalAmostras: 10_000);

            _logger.LogInformation("Modelo treinado com sucesso. AUC: {Auc:P2}, F1: {F1:P2}",
                metrics.Auc, metrics.F1Score);
        }

        var mlContext = new MLContext();
        var model = mlContext.Model.Load(modelPath, out _);
        _predictionEngine = mlContext.Model.CreatePredictionEngine<TransacaoFeatures, TransacaoPrediction>(model);

        _logger.LogInformation("Modelo ML carregado de {Path}", modelPath);
    }

    /// <summary>
    /// Calcula o risco de fraude extraindo features da transação e
    /// passando pelo modelo treinado.
    /// </summary>
    public Task<float> CalcularRiscoAsync(Transacao transacao, Conta? remetente)
    {
        var features = ExtrairFeatures(transacao, remetente);
        var prediction = _predictionEngine.Predict(features);

        _logger.LogDebug(
            "ML Score para transação {Id}: Probability={Prob:F4}, PredictedFraud={IsFraud}",
            transacao.Id, prediction.Probability, prediction.IsFraude);

        return Task.FromResult(prediction.Probability);
    }

    /// <summary>
    /// Extrai as features de uma transação para alimentar o modelo.
    /// Mesmas features usadas no treinamento.
    /// </summary>
    internal static TransacaoFeatures ExtrairFeatures(Transacao transacao, Conta? remetente)
    {
        var hora = transacao.Timestamp.Hour;
        var score = remetente?.Score ?? 50; // Score padrão se conta não encontrada
        var valor = (float)transacao.Valor;

        return new TransacaoFeatures
        {
            Valor = valor,
            HoraDoDia = hora,
            ScoreRemetente = score,
            TransacoesRecentes = 0, // Preenchido pelo caller se disponível
            IsValorRedondo = (valor >= 1000 && valor % 1000 == 0) ? 1f : 0f,
            IsHorarioEstranho = (hora >= 1 && hora <= 5) ? 1f : 0f,
            RazaoValorScore = score > 0 ? valor / score : valor
        };
    }

    public void Dispose()
    {
        _predictionEngine?.Dispose();
    }
}
