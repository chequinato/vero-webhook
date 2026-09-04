namespace Vero.Api.DTOs.Responses;

/// <summary>
/// Resposta do GET /transactions?suspicious=true — listagem de transações suspeitas.
/// </summary>
public class TransacoesSuspeitasResponseDto
{
    public int Total { get; set; }
    public List<TransacaoResumoDto> Transacoes { get; set; } = [];
}

public class TransacaoResumoDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Motivo { get; set; }
}
