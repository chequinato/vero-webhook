namespace Vero.Domain.Entities;

public class TransacaoHistoricoStatus
{
    public int Id { get; set; }

    public string TransacaoId { get; set; } = string.Empty;
    public Transacao? Transacao { get; set; }

    /// <summary>
    /// Status anterior (nulo no primeiro registro da transação).
    /// </summary>
    public string? StatusAnterior { get; set; }

    public string StatusNovo { get; set; } = string.Empty;

    /// <summary>
    /// Motivo da mudança de status (ex: "valor_alto", "velocity").
    /// </summary>
    public string? Motivo { get; set; }

    public DateTime AlteradoEm { get; set; } = DateTime.UtcNow;
}
