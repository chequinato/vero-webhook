using Vero.Domain.Enums;
using Vero.Domain.Policies;

namespace Vero.Tests.Domain;

/// <summary>
/// Regra de negócio pura: sem mock, sem banco, sem HTTP. É o tipo de teste
/// mais barato que existe — e só ficou possível porque a decisão saiu do
/// controller e veio para o Domain.
/// </summary>
public class PoliticaDeRiscoTests
{
    [Theory]
    [InlineData(0.70f)]
    [InlineData(0.99f)]
    public void Decidir_ScoreNoCorteOuAcima_DeveBloquear(float score)
    {
        var (status, motivo) = PoliticaDeRisco.Decidir(score);

        Assert.Equal(StatusTransacao.Bloqueada, status);
        Assert.Equal(PoliticaDeRisco.MotivoBloqueioModelo, motivo);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.69f)]
    public void Decidir_ScoreAbaixoDoCorte_DeveAprovar(float score)
    {
        var (status, motivo) = PoliticaDeRisco.Decidir(score);

        Assert.Equal(StatusTransacao.Aprovada, status);
        Assert.Null(motivo);
    }

    [Theory]
    [InlineData(StatusTransacao.Suspeita, true)]
    [InlineData(StatusTransacao.AceitaProvisoria, true)]
    [InlineData(StatusTransacao.Aprovada, false)]
    [InlineData(StatusTransacao.Bloqueada, false)]
    public void PodeSerReavaliada_SoStatusEmRevisao(StatusTransacao status, bool esperado)
    {
        Assert.Equal(esperado, PoliticaDeRisco.PodeSerReavaliada(status));
    }
}
