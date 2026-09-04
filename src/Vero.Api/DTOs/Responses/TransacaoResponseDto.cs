namespace Vero.Api.DTOs.Responses;

/// <summary>
/// Resposta devolvida ao cliente no POST /transactions.
/// </summary>
public class TransacaoResponseDto
{
    public string Status { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public string? Id { get; set; }
}
