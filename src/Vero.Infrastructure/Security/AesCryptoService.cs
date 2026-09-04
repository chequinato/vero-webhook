using System.Security.Cryptography;
using System.Text;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Security;

/// <summary>
/// Implementação de criptografia AES-256-CBC para dados sensíveis em repouso.
/// A chave é configurada via variável de ambiente ou appsettings (nunca hardcoded).
/// </summary>
public class AesCryptoService : ICryptoService
{
    private readonly byte[] _key;

    /// <summary>
    /// Tamanho do IV (Initialization Vector) do AES em bytes.
    /// </summary>
    private const int IvSize = 16;

    public AesCryptoService(string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentException("Chave de criptografia não pode ser vazia.", nameof(encryptionKey));

        // Deriva uma chave de 256 bits a partir da string fornecida
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Concatena IV + ciphertext (o IV precisa ser armazenado junto)
        var result = new byte[IvSize + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, IvSize);
        Buffer.BlockCopy(cipherBytes, 0, result, IvSize, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        var fullCipher = Convert.FromBase64String(cipherText);

        // Extrai IV e ciphertext
        var iv = new byte[IvSize];
        var cipher = new byte[fullCipher.Length - IvSize];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, IvSize);
        Buffer.BlockCopy(fullCipher, IvSize, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
