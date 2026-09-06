using Microsoft.ML.Data;

namespace Vero.ML.Models;

/// <summary>
/// Features extraídas de uma transação para o modelo de ML.
/// Cada campo é uma variável que o modelo usa para decidir se é fraude.
/// </summary>
public class TransacaoFeatures
{
    /// <summary>Valor da transação em reais.</summary>
    [LoadColumn(0)]
    public float Valor { get; set; }

    /// <summary>Hora do dia (0-23) em que a transação ocorreu.</summary>
    [LoadColumn(1)]
    public float HoraDoDia { get; set; }

    /// <summary>Score de confiança da conta remetente (0-100).</summary>
    [LoadColumn(2)]
    public float ScoreRemetente { get; set; }

    /// <summary>Número de transações do remetente nos últimos 5 minutos.</summary>
    [LoadColumn(3)]
    public float TransacoesRecentes { get; set; }

    /// <summary>1 se o valor é múltiplo redondo de 1000 (>= 1000), 0 caso contrário.</summary>
    [LoadColumn(4)]
    public float IsValorRedondo { get; set; }

    /// <summary>1 se a transação ocorreu entre 1h-5h, 0 caso contrário.</summary>
    [LoadColumn(5)]
    public float IsHorarioEstranho { get; set; }

    /// <summary>Razão valor/score — valores altos com score baixo indicam risco.</summary>
    [LoadColumn(6)]
    public float RazaoValorScore { get; set; }

    /// <summary>Label: true = fraude, false = legítima.</summary>
    [LoadColumn(7)]
    [ColumnName("Label")]
    public bool IsFraude { get; set; }
}
