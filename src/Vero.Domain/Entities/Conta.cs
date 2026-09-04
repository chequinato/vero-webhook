namespace Vero.Domain.Entities;

public class Conta
{
    public int Id { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public string Titular { get; set; } = string.Empty;

    /// <summary>
    /// Score de confiança da conta (escala 0–100).
    /// Fixo/simulado por enquanto — ajuste dinâmico é melhoria futura.
    /// </summary>
    public int Score { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
