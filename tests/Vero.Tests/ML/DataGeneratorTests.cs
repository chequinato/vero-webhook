using Vero.ML.Training;

namespace Vero.Tests.ML;

public class DataGeneratorTests
{
    [Fact]
    public void GerarDataset_DeveRetornarQuantidadeCorreta()
    {
        var dataset = DataGenerator.GerarDataset(1000);

        Assert.Equal(1000, dataset.Count);
    }

    [Fact]
    public void GerarDataset_DeveConterFraudesELegitimas()
    {
        var dataset = DataGenerator.GerarDataset(1000);

        var fraudes = dataset.Count(d => d.IsFraude);
        var legitimas = dataset.Count(d => !d.IsFraude);

        Assert.Equal(500, fraudes);
        Assert.Equal(500, legitimas);
    }

    [Fact]
    public void GerarDataset_ValoresDevemSerPositivos()
    {
        var dataset = DataGenerator.GerarDataset(1000);

        Assert.All(dataset, d =>
        {
            Assert.True(d.Valor >= 0, "Valor deve ser >= 0");
            Assert.InRange(d.HoraDoDia, 0, 23);
            Assert.InRange(d.ScoreRemetente, 0, 100);
            Assert.True(d.TransacoesRecentes >= 0);
        });
    }

    [Fact]
    public void GerarDataset_DeveConterFeaturesDerivadas()
    {
        var dataset = DataGenerator.GerarDataset(1000);

        // Deve haver amostras com horário estranho (entre fraudes)
        var comHorarioEstranho = dataset.Count(d => d.IsHorarioEstranho == 1f);
        Assert.True(comHorarioEstranho > 0, "Deveria haver amostras com horário estranho");

        // Deve haver amostras com valor redondo (entre fraudes)
        var comValorRedondo = dataset.Count(d => d.IsValorRedondo == 1f);
        Assert.True(comValorRedondo > 0, "Deveria haver amostras com valor redondo");

        // RazaoValorScore deve estar preenchida
        Assert.All(dataset, d => Assert.True(d.RazaoValorScore >= 0));
    }
}
