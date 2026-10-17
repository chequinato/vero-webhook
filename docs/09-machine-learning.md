# 09 — Machine Learning com ML.NET

> **🧠 Pra gravar**
> - Regras pegam o **óbvio**; o modelo pega **combinações** de sinais.
> - **Feature** = número que descreve a transação para o modelo. `RazaoValorScore` é uma feature *criada* (feature engineering).
> - **FastTree** = 100 árvores de decisão, cada uma corrigindo os erros das anteriores (gradient boosting). Saída: probabilidade de 0 a 1.
> - `PredictionEngine` **não é thread-safe** → `lock`.

## Para que serve

Regras são determinísticas: "valor > 100 mil → bloqueia". O modelo captura
padrões que combinam várias variáveis — uma transação de R$ 30 mil às 3h de
uma conta de score 40 não viola nenhuma regra isolada, mas o conjunto é
suspeito.

## Features

Cada transação vira um vetor de números:

```csharp
public class TransacaoFeatures
{
    public float Valor;
    public float HoraDoDia;           // 0–23
    public float ScoreRemetente;      // 0–100 (50 se a conta não existir)
    public float TransacoesRecentes;  // velocity: quantas nos últimos 5 min
    public float IsValorRedondo;      // 1 se múltiplo de 1000
    public float IsHorarioEstranho;   // 1 se entre 1h e 5h
    public float RazaoValorScore;     // Valor / Score

    [ColumnName("Label")]
    public bool IsFraude;             // o que o modelo aprende a prever
}
```

**Feature engineering** é criar features úteis. `RazaoValorScore` não existe
nos dados de origem: R$ 50 mil com score 10 (razão 5.000) é muito mais suspeito
que R$ 50 mil com score 90 (razão 555). Uma divisão simples entrega ao modelo
um sinal que ele teria dificuldade de descobrir sozinho.

> **Regra de ouro:** as features no treino e na predição têm que ser calculadas
> **do mesmo jeito**. Por isso a `CalculadoraDeRisco` usa a mesma janela de
> velocity da `VelocityRule`, e a reavaliação recalcula na janela original da
> transação.

## Pipeline de treinamento

```csharp
var mlContext = new MLContext(seed: 42);               // seed fixa = resultado reproduzível

var dataset = DataGenerator.GerarDataset(10_000);      // dados sintéticos
var dataView = mlContext.Data.LoadFromEnumerable(dataset);
var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);   // 80% treino, 20% teste

var pipeline = mlContext.Transforms
    .Concatenate("Features", "Valor", "HoraDoDia", "ScoreRemetente",
        "TransacoesRecentes", "IsValorRedondo", "IsHorarioEstranho", "RazaoValorScore")
    .Append(mlContext.Transforms.NormalizeMinMax("Features"))       // tudo para 0–1
    .Append(mlContext.BinaryClassification.Trainers.FastTree(
        numberOfLeaves: 20, numberOfTrees: 100,
        minimumExampleCountPerLeaf: 10, learningRate: 0.1));

var model = pipeline.Fit(split.TrainSet);
var metrics = mlContext.BinaryClassification.Evaluate(model.Transform(split.TestSet));
mlContext.Model.Save(model, dataView.Schema, modelPath);
```

**Por que separar treino e teste?** Avaliar o modelo com os mesmos dados que
ele aprendeu é como corrigir a prova com o gabarito na mão: sempre parece ótimo.
Os 20% guardados medem se ele **generaliza**.

**Métricas:** *accuracy* (acertos no total), *AUC* (capacidade de separar
fraude de não fraude, de 0,5 = chute a 1,0 = perfeito) e *F1* (equilíbrio entre
não deixar passar fraude e não acusar inocente). Em fraude, accuracy engana:
com 90% de transações legítimas, um modelo que diz "nunca é fraude" acerta 90%.

## FastTree — gradient boosted decision trees

- **Árvore de decisão:** uma sequência de perguntas binárias ("valor > 50 mil?", "hora < 5?").
- **Ensemble:** 100 árvores votando juntas são mais robustas que uma.
- **Boosting:** cada árvore nova foca nos erros das anteriores.
- **Saída:** `Probability`, entre 0.0 e 1.0.

## Dados sintéticos

Sem dados reais de fraude, o `DataGenerator` fabrica um dataset com **90%
legítimas e 10% fraude**, em seis padrões: valor alto, score baixo, velocity,
horário estranho, valor redondo e combinações. Um ruído aleatório é somado para
o modelo não decorar valores exatos.

> **Limite honesto:** o modelo aprende os padrões que **nós** escrevemos no
> gerador. Ele reproduz as regras com mais nuance, mas não descobre fraude nova.
> Para isso seriam necessários dados reais rotulados.

## Predição em tempo de execução

```csharp
public Task<float> CalcularRiscoAsync(Transacao transacao, Conta? remetente, int transacoesRecentes = 0)
{
    var features = ExtrairFeatures(transacao, remetente, transacoesRecentes);

    TransacaoPrediction prediction;
    lock (_predictionLock)                 // PredictionEngine não é thread-safe
    {
        prediction = _predictionEngine.Predict(features);
    }

    return Task.FromResult(prediction.Probability);
}
```

O serviço é **singleton** (carregar o modelo é caro), então várias requisições
o usam ao mesmo tempo. Sem o `lock`, chamadas simultâneas a `Predict()`
corromperiam o estado interno do engine.

## Onde o modelo vive

Se o arquivo `Models/fraud_model.zip` não existir quando a API sobe, o
`MlRiskScoringService` **treina um modelo na hora** com 10 mil amostras e salva.
A primeira subida demora alguns segundos a mais; as seguintes carregam o arquivo.

O caminho pode ser trocado com a configuração `ML:ModelPath`.

---

**Anterior:** [← 08 — Segurança](08-seguranca.md) · **Próximo:** [10 — Testes →](10-testes.md)
