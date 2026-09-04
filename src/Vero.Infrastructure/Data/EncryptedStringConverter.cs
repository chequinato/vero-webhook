using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Vero.Domain.Interfaces;

namespace Vero.Infrastructure.Data;

/// <summary>
/// Value converter do EF Core que criptografa/descriptografa strings automaticamente
/// ao salvar/ler do banco. Aplicado nos campos sensíveis (NumeroConta, Titular).
/// </summary>
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(ICryptoService cryptoService)
        : base(
            v => cryptoService.Encrypt(v),       // ao salvar no banco
            v => cryptoService.Decrypt(v))        // ao ler do banco
    {
    }
}
