using System.Security.Cryptography;
using System.Text;

namespace Vero.Api.Middleware;

/// <summary>
/// Middleware que valida a assinatura HMAC-SHA256 do payload recebido no webhook.
/// O remetente assina o body com uma chave secreta compartilhada e envia no header X-Signature.
/// A API recalcula a assinatura e só processa se bater.
/// </summary>
public class HmacAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _secret;
    private readonly ILogger<HmacAuthenticationMiddleware> _logger;

    /// <summary>
    /// Header onde a assinatura HMAC é esperada.
    /// </summary>
    public const string SignatureHeader = "X-Signature";

    public HmacAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<HmacAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _secret = configuration["Hmac:Secret"]
            ?? Environment.GetEnvironmentVariable("VERO_HMAC_SECRET")
            ?? throw new InvalidOperationException(
                "HMAC secret não configurado. Defina 'Hmac:Secret' no appsettings ou a variável VERO_HMAC_SECRET.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Só aplica HMAC em rotas de webhook (POST /transactions)
        if (!IsWebhookRoute(context))
        {
            await _next(context);
            return;
        }

        // Verifica se o header de assinatura existe
        if (!context.Request.Headers.TryGetValue(SignatureHeader, out var signatureHeader))
        {
            _logger.LogWarning("Requisição sem header {Header} recusada.", SignatureHeader);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { erro = "Header de assinatura ausente." });
            return;
        }

        // Lê o body (habilita buffering para que o body possa ser lido novamente pelo controller)
        context.Request.EnableBuffering();
        var body = await ReadBodyAsync(context.Request);

        // Calcula a assinatura esperada
        var assinaturaEsperada = CalcularHmac(body);
        var assinaturaRecebida = signatureHeader.ToString();

        // Comparação em tempo constante para evitar timing attacks
        if (!CompararEmTempoConstante(assinaturaEsperada, assinaturaRecebida))
        {
            _logger.LogWarning("Assinatura HMAC inválida recusada.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { erro = "Assinatura inválida." });
            return;
        }

        // Rebobina o body para o controller poder ler
        context.Request.Body.Position = 0;
        await _next(context);
    }

    private static bool IsWebhookRoute(HttpContext context)
    {
        return context.Request.Method == HttpMethods.Post
            && context.Request.Path.StartsWithSegments("/transactions", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private string CalcularHmac(string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Comparação em tempo constante — protege contra timing attacks.
    /// </summary>
    private static bool CompararEmTempoConstante(string a, string b)
    {
        if (a.Length != b.Length) return false;

        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);

        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}

/// <summary>
/// Extension method para registrar o middleware no pipeline.
/// </summary>
public static class HmacAuthenticationMiddlewareExtensions
{
    public static IApplicationBuilder UseHmacAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HmacAuthenticationMiddleware>();
    }
}
