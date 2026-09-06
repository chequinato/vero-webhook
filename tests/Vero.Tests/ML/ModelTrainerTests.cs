using Vero.ML.Training;

namespace Vero.Tests.ML;

public class ModelTrainerTests : IDisposable
{
    private readonly string _tempModelPath;

    public ModelTrainerTests()
    {
        _tempModelPath = Path.Combine(Path.GetTempPath(), $"vero_test_model_{Guid.NewGuid()}.zip");
    }

    public void Dispose()
    {
        if (File.Exists(_tempModelPath))
            File.Delete(_tempModelPath);
    }

    [Fact]
    public void TreinarESalvar_DeveCriarArquivoDoModelo()
    {
        var trainer = new ModelTrainer();

        var metrics = trainer.TreinarESalvar(_tempModelPath, totalAmostras: 500);

        Assert.True(File.Exists(_tempModelPath));
        Assert.True(new FileInfo(_tempModelPath).Length > 0);
    }

    [Fact]
    public void TreinarESalvar_AucDeveSerAcimaDe80Porcento()
    {
        var trainer = new ModelTrainer();

        var metrics = trainer.TreinarESalvar(_tempModelPath, totalAmostras: 2000);

        // O modelo deve ter AUC > 0.80 com dados sintéticos bem separados
        Assert.True(metrics.Auc > 0.80,
            $"AUC esperado > 0.80, obtido: {metrics.Auc:P2}");
    }

    [Fact]
    public void TreinarESalvar_AccuracyDeveSerAcimaDe75Porcento()
    {
        var trainer = new ModelTrainer();

        var metrics = trainer.TreinarESalvar(_tempModelPath, totalAmostras: 2000);

        Assert.True(metrics.Accuracy > 0.75,
            $"Accuracy esperado > 0.75, obtido: {metrics.Accuracy:P2}");
    }

    [Fact]
    public void TreinarESalvar_MetricasDevemSerPreenchidas()
    {
        var trainer = new ModelTrainer();

        var metrics = trainer.TreinarESalvar(_tempModelPath, totalAmostras: 500);

        Assert.True(metrics.Accuracy > 0);
        Assert.True(metrics.Auc > 0);
        Assert.True(metrics.F1Score > 0);
        Assert.True(metrics.Precision > 0);
        Assert.True(metrics.Recall > 0);
        Assert.Equal(_tempModelPath, metrics.ModelPath);
    }
}
