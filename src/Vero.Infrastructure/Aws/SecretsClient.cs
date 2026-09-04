using Microsoft.Extensions.Logging;

namespace Vero.Infrastructure.Aws;

/// <summary>
/// Client para AWS Secrets Manager.
/// Implementado para estudo; na prática o projeto roda com variáveis de ambiente.
///
/// TODO: Implementar quando quiser usar Secrets Manager real.
/// </summary>
public class SecretsClient
{
    private readonly ILogger<SecretsClient> _logger;

    public SecretsClient(ILogger<SecretsClient> logger)
    {
        _logger = logger;
    }

    public Task<string> ObterSegredoAsync(string secretName)
    {
        // TODO: Implementar com Amazon.SecretsManager SDK
        // var request = new GetSecretValueRequest { SecretId = secretName };
        // var response = await _secretsClient.GetSecretValueAsync(request);
        // return response.SecretString;
        _logger.LogInformation(
            "SecretsManager: Buscaria segredo {SecretName} — usando variável de ambiente como fallback",
            secretName);
        return Task.FromResult(Environment.GetEnvironmentVariable(secretName) ?? string.Empty);
    }
}
