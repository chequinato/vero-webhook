namespace Vero.Domain.Rules;

/// <summary>
/// Regra barata, avaliada no momento em que o webhook chega.
/// Se for violada, a transação é bloqueada na hora (HTTP 403).
///
/// Por que uma interface só pra isso? Antes, regras síncronas e assíncronas
/// eram todas <see cref="IRule"/> e quem decidia o "tipo" era o Program.cs
/// que as registrava. Bastava registrar errado para uma regra pesada rodar
/// no caminho síncrono. Com uma interface por tipo, o próprio compilador
/// garante que cada serviço recebe só as regras que fazem sentido para ele
/// (Interface Segregation Principle).
/// </summary>
public interface IRegraSincrona : IRule;
