using Vero.Domain.Enums;

namespace Vero.Domain.Entities;

public class Transacao
{
    /// <summary>
    /// Identificador vindo do payload do webhook (ex: "txn_001").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public int RemetenteId { get; set; }
    public Conta? Remetente { get; set; }

    public int DestinatarioId { get; set; }
    public Conta? Destinatario { get; set; }

    public decimal Valor { get; set; }

    /// <summary>
    /// Tipo da transação (ex: "pix").
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Moeda da transação (ex: "BRL").
    /// </summary>
    public string Moeda { get; set; } = string.Empty;

    /// <summary>
    /// Momento em que a transação ocorreu (vem do payload).
    /// </summary>
    public DateTime Timestamp { get; set; }

    public StatusTransacao Status { get; set; }

    /// <summary>
    /// Motivo do bloqueio ou suspeita (ex: "valor_alto"). Nulo se aprovada.
    /// </summary>
    public string? Motivo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
