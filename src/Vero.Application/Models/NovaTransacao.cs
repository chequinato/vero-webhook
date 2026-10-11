namespace Vero.Application.Models;

/// <summary>
/// Dados de entrada para receber uma transação.
///
/// Por que não usar o TransacaoRequestDto da Api direto? Porque a Application
/// não pode depender da Api (a seta de dependência aponta sempre para dentro).
/// O controller converte o DTO HTTP neste record e a Application nem sabe que
/// existe HTTP — o mesmo serviço poderia ser chamado por uma fila ou um CLI.
/// </summary>
public record NovaTransacao(
    string Id,
    decimal Valor,
    string NumeroContaRemetente,
    string NumeroContaDestinatario,
    string Tipo,
    string Moeda,
    DateTime Timestamp);
