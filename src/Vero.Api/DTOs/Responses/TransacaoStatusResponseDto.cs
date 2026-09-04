namespace Vero.Api.DTOs.Responses;

/// <summary>
/// Resposta do GET /transactions/{id} — status detalhado de uma transação.
/// </summary>
public class TransacaoStatusResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public DateTime? AnalisadaEm { get; set; }
}
