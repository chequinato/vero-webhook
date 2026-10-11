using Vero.Domain.Enums;

namespace Vero.Domain.Policies;

/// <summary>
/// Regras de negócio sobre o score do modelo de ML.
///
/// Antes essa decisão (o corte de 70% e quais status podem ser reavaliados)
/// morava dentro do DashboardController. Controller é camada de entrega HTTP:
/// se amanhã a reavaliação vier de uma fila ou de um job, a regra teria que
/// ser copiada. Aqui ela fica no Domain, pura, sem dependências e fácil de
/// testar (Single Responsibility Principle).
/// </summary>
public static class PoliticaDeRisco
{
    /// <summary>
    /// Corte de bloqueio do modelo. É o mesmo 70% que o painel imprime sobre
    /// a distribuição de risco — se mudar aqui, mude lá também.
    /// </summary>
    public const float LimiarBloqueio = 0.70f;

    public const string MotivoBloqueioModelo = "modelo: risco acima do corte";

    /// <summary>
    /// Só transações ainda em revisão podem ser reavaliadas. Reavaliar algo
    /// já decidido permitiria desfazer um bloqueio por fora de um fluxo próprio.
    /// </summary>
    public static bool PodeSerReavaliada(StatusTransacao status) =>
        status is StatusTransacao.Suspeita or StatusTransacao.AceitaProvisoria;

    /// <summary>
    /// Score no corte ou acima vira bloqueada; abaixo, aprovada.
    /// </summary>
    public static (StatusTransacao Status, string? Motivo) Decidir(float score) =>
        score >= LimiarBloqueio
            ? (StatusTransacao.Bloqueada, MotivoBloqueioModelo)
            : (StatusTransacao.Aprovada, null);
}
