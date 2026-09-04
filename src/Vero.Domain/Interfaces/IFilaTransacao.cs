namespace Vero.Domain.Interfaces;

/// <summary>
/// Contrato para a fila de eventos (desacopla recepção da análise assíncrona).
/// A implementação real usa SQS; em desenvolvimento local, pode ser uma fila em memória.
/// </summary>
public interface IFilaTransacao
{
    /// <summary>
    /// Envia o ID da transação para a fila de análise assíncrona.
    /// </summary>
    Task EnviarParaAnaliseAsync(string transacaoId);

    /// <summary>
    /// Consome a próxima mensagem da fila (usada pelo worker).
    /// </summary>
    /// <returns>O ID da transação a ser analisada, ou null se a fila estiver vazia.</returns>
    Task<string?> ConsumirProximaAsync();
}
