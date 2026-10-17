namespace Vero.Domain.Rules;

/// <summary>
/// Regra mais pesada (pode consultar o banco), avaliada em background pelo
/// Worker depois que a transação já foi aceita provisoriamente.
/// Se for violada, a transação vira "suspeita" e um alerta é disparado.
/// </summary>
public interface IRegraAssincrona : IRule;
