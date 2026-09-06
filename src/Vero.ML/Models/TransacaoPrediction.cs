using Microsoft.ML.Data;

namespace Vero.ML.Models;

/// <summary>
/// Saída do modelo de ML — predição de fraude com probabilidade.
/// </summary>
public class TransacaoPrediction
{
    /// <summary>Classificação binária: true = fraude prevista.</summary>
    [ColumnName("PredictedLabel")]
    public bool IsFraude { get; set; }

    /// <summary>Probabilidade de ser fraude (0.0 a 1.0) — usado como RiskScore.</summary>
    public float Probability { get; set; }

    /// <summary>Score bruto do modelo (log-odds).</summary>
    public float Score { get; set; }
}
