using Vero.ML.Models;

namespace Vero.ML.Training;

/// <summary>
/// Gera dados sintéticos de treinamento para o modelo de detecção de fraude.
/// Os padrões são baseados nas regras existentes do sistema, com ruído
/// adicionado para que o modelo aprenda a generalizar.
///
/// A distribuição padrão é 90% legítimas / 10% fraudes, mais próxima da
/// realidade. O modelo é treinado com FastTree que lida bem com classes
/// desbalanceadas quando a separação é clara.
/// </summary>
public static class DataGenerator
{
    private static readonly Random _random = new(42); // Seed fixa para reprodutibilidade

    /// <summary>
    /// Gera um dataset de treinamento com transações legítimas e fraudulentas.
    /// </summary>
    /// <param name="totalAmostras">Total de amostras a gerar</param>
    /// <param name="fracaoFraude">Fração de amostras fraudulentas (padrão: 10%)</param>
    public static List<TransacaoFeatures> GerarDataset(int totalAmostras = 10_000, double fracaoFraude = 0.10)
    {
        var dataset = new List<TransacaoFeatures>();
        var totalFraudes = (int)(totalAmostras * fracaoFraude);
        var totalLegitimas = totalAmostras - totalFraudes;

        // ──────────────────────────────────────────────────────────
        // Transações LEGÍTIMAS
        // ──────────────────────────────────────────────────────────
        for (int i = 0; i < totalLegitimas; i++)
        {
            dataset.Add(GerarTransacaoLegitima());
        }

        // ──────────────────────────────────────────────────────────
        // Transações FRAUDULENTAS (baseadas nos padrões das regras)
        // ──────────────────────────────────────────────────────────
        for (int i = 0; i < totalFraudes; i++)
        {
            dataset.Add(GerarTransacaoFraudulenta());
        }

        // Embaralhar para evitar viés de ordem
        return dataset.OrderBy(_ => _random.Next()).ToList();
    }

    private static TransacaoFeatures GerarTransacaoLegitima()
    {
        // Valores normais: 10 a 30.000, scores altos, horários comerciais
        var valor = (float)(_random.NextDouble() * 29_990 + 10);
        var hora = _random.Next(6, 24); // 6h-23h (horário normal)
        var score = _random.Next(50, 100); // Score médio-alto
        var txRecentes = _random.Next(0, 2); // Poucas transações recentes

        return new TransacaoFeatures
        {
            Valor = valor,
            HoraDoDia = hora,
            ScoreRemetente = score,
            TransacoesRecentes = txRecentes,
            IsValorRedondo = (valor >= 1000 && valor % 1000 == 0) ? 1f : 0f,
            IsHorarioEstranho = (hora >= 1 && hora <= 5) ? 1f : 0f,
            RazaoValorScore = score > 0 ? valor / score : valor,
            IsFraude = false
        };
    }

    private static TransacaoFeatures GerarTransacaoFraudulenta()
    {
        // Seleciona aleatoriamente um padrão de fraude
        var padrao = _random.Next(0, 6);

        return padrao switch
        {
            0 => GerarFraude_ValorAlto(),
            1 => GerarFraude_ScoreBaixo(),
            2 => GerarFraude_Velocity(),
            3 => GerarFraude_HorarioEstranho(),
            4 => GerarFraude_ValorRedondo(),
            _ => GerarFraude_Combinada()
        };
    }

    // Padrão 1: Valor muito alto (> 100k)
    private static TransacaoFeatures GerarFraude_ValorAlto()
    {
        var valor = (float)(_random.NextDouble() * 400_000 + 100_001);
        var hora = _random.Next(0, 24);
        var score = _random.Next(20, 80);
        var txRecentes = _random.Next(0, 3);

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    // Padrão 2: Score de confiança muito baixo (< 35)
    private static TransacaoFeatures GerarFraude_ScoreBaixo()
    {
        var valor = (float)(_random.NextDouble() * 50_000 + 500);
        var hora = _random.Next(0, 24);
        var score = _random.Next(1, 35); // Score baixo
        var txRecentes = _random.Next(0, 4);

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    // Padrão 3: Muitas transações num curto período (velocity)
    private static TransacaoFeatures GerarFraude_Velocity()
    {
        var valor = (float)(_random.NextDouble() * 20_000 + 100);
        var hora = _random.Next(0, 24);
        var score = _random.Next(30, 70);
        var txRecentes = _random.Next(3, 10); // Muitas transações recentes

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    // Padrão 4: Transação em horário estranho (1h-5h)
    private static TransacaoFeatures GerarFraude_HorarioEstranho()
    {
        var valor = (float)(_random.NextDouble() * 30_000 + 500);
        var hora = _random.Next(1, 6); // 1h-5h
        var score = _random.Next(30, 65);
        var txRecentes = _random.Next(0, 3);

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    // Padrão 5: Valor redondo suspeitamente alto
    private static TransacaoFeatures GerarFraude_ValorRedondo()
    {
        var multiplicador = _random.Next(1, 50);
        var valor = (float)(multiplicador * 1000);
        var hora = _random.Next(0, 24);
        var score = _random.Next(30, 60);
        var txRecentes = _random.Next(0, 3);

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    // Padrão combinado: múltiplos sinais juntos
    private static TransacaoFeatures GerarFraude_Combinada()
    {
        var valor = (float)(_random.NextDouble() * 80_000 + 5000);
        var hora = _random.Next(1, 6); // Horário estranho
        var score = _random.Next(10, 40); // Score baixo
        var txRecentes = _random.Next(2, 6); // Velocity moderada

        return CriarFeatures(valor, hora, score, txRecentes, true);
    }

    private static TransacaoFeatures CriarFeatures(
        float valor, int hora, int score, int txRecentes, bool isFraude)
    {
        // Adicionar ruído gaussiano leve para generalização
        valor = Math.Max(0, valor + (float)(_random.NextDouble() * 200 - 100));
        var scoreF = Math.Clamp(score + _random.Next(-5, 6), 0, 100);

        return new TransacaoFeatures
        {
            Valor = valor,
            HoraDoDia = hora,
            ScoreRemetente = scoreF,
            TransacoesRecentes = txRecentes,
            IsValorRedondo = (valor >= 1000 && valor % 1000 < 50) ? 1f : 0f,
            IsHorarioEstranho = (hora >= 1 && hora <= 5) ? 1f : 0f,
            RazaoValorScore = scoreF > 0 ? valor / scoreF : valor,
            IsFraude = isFraude
        };
    }
}
