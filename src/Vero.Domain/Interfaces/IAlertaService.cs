namespace Vero.Domain.Interfaces;

/// <summary>
/// Contrato para disparo de alertas quando uma anomalia é confirmada.
/// A implementação real usa SNS.
/// </summary>
public interface IAlertaService
{
    Task EnviarAlertaAsync(string transacaoId, string motivo);
}
