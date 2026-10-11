using Vero.Domain.Entities;

namespace Vero.Application.Models;

public enum DesfechoRecebimento
{
    Aceita,
    Bloqueada,
    Duplicada,
    ContaNaoEncontrada
}

/// <summary>
/// Resultado de receber uma transação (Result Pattern).
///
/// Em vez de lançar exceção para casos esperados ("conta não existe",
/// "transação repetida") ou devolver tuplas soltas, o serviço devolve um
/// objeto que diz explicitamente o que aconteceu. O controller só traduz
/// cada desfecho para um status HTTP.
/// </summary>
public record ResultadoRecebimento(DesfechoRecebimento Desfecho, Transacao? Transacao)
{
    public static ResultadoRecebimento Aceita(Transacao t) => new(DesfechoRecebimento.Aceita, t);
    public static ResultadoRecebimento Bloqueada(Transacao t) => new(DesfechoRecebimento.Bloqueada, t);
    public static ResultadoRecebimento Duplicada(Transacao existente) => new(DesfechoRecebimento.Duplicada, existente);
    public static ResultadoRecebimento ContaNaoEncontrada() => new(DesfechoRecebimento.ContaNaoEncontrada, null);
}
