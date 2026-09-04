using System.Security.Cryptography;
using System.Text;

namespace Vero.Tests.Security;

/// <summary>
/// Testes do cálculo de HMAC — mesma lógica usada no middleware e no simulador.
/// </summary>
public class HmacTests
{
    private const string Secret = "test-secret";

    [Fact]
    public void CalcularHmac_MesmoPayload_MesmaChave_DeveDarMesmoResultado()
    {
        var payload = "{\"id\":\"txn_001\",\"valor\":1500}";

        var hash1 = CalcularHmac(payload, Secret);
        var hash2 = CalcularHmac(payload, Secret);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void CalcularHmac_PayloadsDiferentes_DevemDarResultadosDiferentes()
    {
        var payload1 = "{\"id\":\"txn_001\",\"valor\":1500}";
        var payload2 = "{\"id\":\"txn_002\",\"valor\":1500}";

        var hash1 = CalcularHmac(payload1, Secret);
        var hash2 = CalcularHmac(payload2, Secret);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalcularHmac_ChavesDiferentes_DevemDarResultadosDiferentes()
    {
        var payload = "{\"id\":\"txn_001\",\"valor\":1500}";

        var hash1 = CalcularHmac(payload, "chave-1");
        var hash2 = CalcularHmac(payload, "chave-2");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void CalcularHmac_DeveRetornarHexadecimalMinusculo()
    {
        var payload = "test";
        var hash = CalcularHmac(payload, Secret);

        Assert.Matches("^[0-9a-f]+$", hash);
        Assert.Equal(64, hash.Length); // SHA-256 = 32 bytes = 64 hex chars
    }

    private static string CalcularHmac(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }
}
