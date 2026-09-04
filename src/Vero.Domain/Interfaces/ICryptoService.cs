namespace Vero.Domain.Interfaces;

/// <summary>
/// Contrato para criptografia de dados sensíveis em repouso.
/// Dados como NumeroConta remetente/destinatário ficam criptografados no banco.
/// </summary>
public interface ICryptoService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
