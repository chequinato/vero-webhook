namespace Vero.Domain.Entities;

/// <summary>
/// Estatísticas agregadas de transações (para dashboard).
/// </summary>
public record TransacaoStats
{
    public int Total { get; init; }
    public int Aprovadas { get; init; }
    public int Bloqueadas { get; init; }
    public int Suspeitas { get; init; }
    public int AceitasProvisoria { get; init; }
    public decimal ValorTotal { get; init; }
    public decimal ValorMedio { get; init; }
    public float RiskScoreMedio { get; init; }
}

/// <summary>
/// Volume de transações numa hora específica.
/// </summary>
public record VolumeHora
{
    public DateTime Hora { get; init; }
    public int Quantidade { get; init; }
    public decimal Valor { get; init; }
    public int Bloqueadas { get; init; }
    public int Suspeitas { get; init; }
}
