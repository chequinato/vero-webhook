using Microsoft.ML;
using Microsoft.ML.Data;
using Vero.ML.Models;

namespace Vero.ML.Training;

/// <summary>
/// Treina e avalia o modelo de detecção de fraude usando ML.NET.
///
/// Pipeline: Features → Normalização → FastTree (gradient boosting) → Calibração
///
/// O FastTree é um algoritmo de gradient boosted trees — excelente para dados
/// tabulares com features numéricas, rápido para treinar, e produz probabilidades
/// calibradas (úteis como risk score).
/// </summary>
public class ModelTrainer
{
    private readonly MLContext _mlContext;

    public ModelTrainer(int? seed = 42)
    {
        _mlContext = new MLContext(seed: seed);
    }

    /// <summary>
    /// Treina o modelo com dados sintéticos e retorna as métricas de avaliação.
    /// </summary>
    /// <param name="modelPath">Caminho para salvar o modelo treinado (.zip)</param>
    /// <param name="totalAmostras">Total de amostras de treinamento</param>
    /// <returns>Métricas de avaliação (AUC, accuracy, F1, etc.)</returns>
    public ModelMetrics TreinarESalvar(string modelPath, int totalAmostras = 10_000)
    {
        // 1. Gerar dados sintéticos
        var dataset = DataGenerator.GerarDataset(totalAmostras);

        // 2. Carregar no ML.NET
        var dataView = _mlContext.Data.LoadFromEnumerable(dataset);

        // 3. Split: 80% treino, 20% teste
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

        // 4. Montar o pipeline de treinamento
        var pipeline = _mlContext.Transforms
            // Concatenar todas as features num vetor
            .Concatenate("Features",
                nameof(TransacaoFeatures.Valor),
                nameof(TransacaoFeatures.HoraDoDia),
                nameof(TransacaoFeatures.ScoreRemetente),
                nameof(TransacaoFeatures.TransacoesRecentes),
                nameof(TransacaoFeatures.IsValorRedondo),
                nameof(TransacaoFeatures.IsHorarioEstranho),
                nameof(TransacaoFeatures.RazaoValorScore))
            // Normalizar features para mesma escala
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            // Classificador: FastTree (gradient boosted decision trees)
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 10,
                learningRate: 0.1));

        // 5. Treinar
        var model = pipeline.Fit(split.TrainSet);

        // 6. Avaliar no conjunto de teste
        var predictions = model.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, "Label");

        // 7. Salvar o modelo
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        _mlContext.Model.Save(model, dataView.Schema, modelPath);

        return new ModelMetrics
        {
            Accuracy = metrics.Accuracy,
            Auc = metrics.AreaUnderRocCurve,
            F1Score = metrics.F1Score,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            ModelPath = modelPath
        };
    }
}

/// <summary>
/// Métricas de avaliação do modelo treinado.
/// </summary>
public record ModelMetrics
{
    public double Accuracy { get; init; }
    public double Auc { get; init; }
    public double F1Score { get; init; }
    public double Precision { get; init; }
    public double Recall { get; init; }
    public string ModelPath { get; init; } = string.Empty;

    public override string ToString() =>
        $"""
        ════════════════════════════════════════
        Métricas do Modelo de Detecção de Fraude
        ════════════════════════════════════════
        Accuracy:  {Accuracy:P2}
        AUC:       {Auc:P2}
        F1 Score:  {F1Score:P2}
        Precision: {Precision:P2}
        Recall:    {Recall:P2}
        Modelo:    {ModelPath}
        ════════════════════════════════════════
        """;
}
