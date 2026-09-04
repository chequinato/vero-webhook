using Vero.Infrastructure.Security;

namespace Vero.Tests.Security;

public class AesCryptoServiceTests
{
    private const string TestKey = "chave-de-teste-para-unit-tests";

    [Fact]
    public void Encrypt_Decrypt_DeveRetornarValorOriginal()
    {
        var crypto = new AesCryptoService(TestKey);
        var original = "conta_123";

        var encrypted = crypto.Encrypt(original);
        var decrypted = crypto.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_DeveGerarCiphertextDiferenteDoOriginal()
    {
        var crypto = new AesCryptoService(TestKey);
        var original = "Maria Silva";

        var encrypted = crypto.Encrypt(original);

        Assert.NotEqual(original, encrypted);
    }

    [Fact]
    public void Encrypt_MesmoTexto_DeveGerarCiphertextsDiferentes()
    {
        // Cada criptografia usa um IV aleatório diferente
        var crypto = new AesCryptoService(TestKey);
        var original = "conta_456";

        var encrypted1 = crypto.Encrypt(original);
        var encrypted2 = crypto.Encrypt(original);

        Assert.NotEqual(encrypted1, encrypted2);
        // Mas ambos descriptografam pro mesmo valor
        Assert.Equal(original, crypto.Decrypt(encrypted1));
        Assert.Equal(original, crypto.Decrypt(encrypted2));
    }

    [Fact]
    public void Encrypt_StringVazia_DeveRetornarStringVazia()
    {
        var crypto = new AesCryptoService(TestKey);

        var encrypted = crypto.Encrypt(string.Empty);

        Assert.Equal(string.Empty, encrypted);
    }

    [Fact]
    public void Decrypt_ComChaveDiferente_DeveFalhar()
    {
        var crypto1 = new AesCryptoService("chave-1");
        var crypto2 = new AesCryptoService("chave-2");

        var encrypted = crypto1.Encrypt("dado sensível");

        Assert.ThrowsAny<Exception>(() => crypto2.Decrypt(encrypted));
    }

    [Fact]
    public void Constructor_ChaveVazia_DeveLancarExcecao()
    {
        Assert.Throws<ArgumentException>(() => new AesCryptoService(""));
        Assert.Throws<ArgumentException>(() => new AesCryptoService("   "));
    }

    [Theory]
    [InlineData("texto curto")]
    [InlineData("João da Silva Oliveira Santos")]
    [InlineData("conta_com_caracteres_especiais_ção_ñ_ü")]
    [InlineData("12345678901234567890")] // CPF-like
    public void Encrypt_Decrypt_DeveFuncionarComDiversosTextos(string original)
    {
        var crypto = new AesCryptoService(TestKey);

        var encrypted = crypto.Encrypt(original);
        var decrypted = crypto.Decrypt(encrypted);

        Assert.Equal(original, decrypted);
    }
}
