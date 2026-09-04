using System.ComponentModel.DataAnnotations;

namespace Vero.Api.DTOs.Requests;

/// <summary>
/// Payload que chega no webhook POST /transactions.
/// </summary>
public class TransacaoRequestDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required]
    public string Remetente { get; set; } = string.Empty;

    [Required]
    public string Destinatario { get; set; } = string.Empty;

    [Required]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public string Moeda { get; set; } = string.Empty;
}
