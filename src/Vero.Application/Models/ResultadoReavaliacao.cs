using Vero.Domain.Entities;

namespace Vero.Application.Models;

public enum DesfechoReavaliacao
{
    Reavaliada,
    NaoEncontrada,
    JaDecidida
}

/// <summary>
/// Resultado de pedir ao modelo uma nova avaliação (Result Pattern).
/// </summary>
public record ResultadoReavaliacao(DesfechoReavaliacao Desfecho, Transacao? Transacao)
{
    public static ResultadoReavaliacao Reavaliada(Transacao t) => new(DesfechoReavaliacao.Reavaliada, t);
    public static ResultadoReavaliacao JaDecidida(Transacao t) => new(DesfechoReavaliacao.JaDecidida, t);
    public static ResultadoReavaliacao NaoEncontrada() => new(DesfechoReavaliacao.NaoEncontrada, null);
}
