using Vero.Api.DTOs.Requests;
using Vero.Application.Models;
using Vero.Domain.Enums;

namespace Vero.Api.Mappers;

/// <summary>
/// Conversões entre o mundo HTTP (DTOs) e o mundo da Application (Mapper Pattern).
///
/// Antes cada controller fazia <c>Status.ToString().ToLowerInvariant()</c> e
/// montava objetos na mão em vários lugares. Centralizar aqui garante que a
/// API fale o mesmo "idioma" em todas as rotas.
/// </summary>
public static class TransacaoMapper
{
    public static NovaTransacao ParaNovaTransacao(this TransacaoRequestDto dto) => new(
        Id: dto.Id,
        Valor: dto.Valor,
        NumeroContaRemetente: dto.Remetente,
        NumeroContaDestinatario: dto.Destinatario,
        Tipo: dto.Tipo,
        Moeda: dto.Moeda,
        Timestamp: dto.Timestamp);

    /// <summary>
    /// Texto do status como a API e o dashboard já conhecem (ex: "aprovada",
    /// "aceitaprovisoria").
    /// </summary>
    public static string ParaTexto(this StatusTransacao status) =>
        status.ToString().ToLowerInvariant();
}
