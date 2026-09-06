using System.Collections.Concurrent;

namespace Vero.Api.Middleware;

/// <summary>
/// Middleware de rate limiting por IP usando sliding window.
/// Limita o número de requisições por IP num intervalo de tempo para evitar flood/abuso.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _limiteRequisicoes;
    private readonly TimeSpan _janelaTempo;

    /// <summary>
    /// Armazena os timestamps de requisições por IP.
    /// ConcurrentDictionary garante thread-safety.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SlidingWindow> _janelas = new();

    /// <summary>
    /// Timer que limpa IPs inativos periodicamente para evitar memory leak.
    /// </summary>
    private static readonly Timer _cleanupTimer = new(LimparJanelasExpiradas, null,
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _limiteRequisicoes = configuration.GetValue("RateLimiting:MaxRequests", 60);
        _janelaTempo = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:WindowSeconds", 60));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = ObterIp(context);
        var janela = _janelas.GetOrAdd(ip, _ => new SlidingWindow());

        if (!janela.TentarRegistrar(_limiteRequisicoes, _janelaTempo))
        {
            _logger.LogWarning(
                "Rate limit atingido para IP {Ip}: {Limite} requisições em {Janela}s.",
                ip, _limiteRequisicoes, _janelaTempo.TotalSeconds);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = _janelaTempo.TotalSeconds.ToString("F0");
            await context.Response.WriteAsJsonAsync(new
            {
                erro = "Limite de requisições excedido. Tente novamente em breve.",
                retry_after_seconds = (int)_janelaTempo.TotalSeconds
            });
            return;
        }

        await _next(context);
    }

    private static string ObterIp(HttpContext context)
    {
        // Verifica X-Forwarded-For primeiro (proxy/load balancer)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Remove IPs cujo sliding window está vazio (sem requisições recentes).
    /// Chamado periodicamente pelo timer para evitar crescimento ilimitado da memória.
    /// </summary>
    private static void LimparJanelasExpiradas(object? state)
    {
        foreach (var kvp in _janelas)
        {
            if (kvp.Value.EstaVazio)
            {
                _janelas.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Sliding window — mantém os timestamps das requisições recentes e descarta as expiradas.
/// Thread-safe via lock.
/// </summary>
internal class SlidingWindow
{
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    /// <summary>
    /// Indica se a janela não contém timestamps (IP inativo).
    /// </summary>
    public bool EstaVazio
    {
        get
        {
            lock (_lock)
            {
                // Limpa expirados antes de verificar
                var limiteInferior = DateTime.UtcNow.AddMinutes(-2);
                while (_timestamps.Count > 0 && _timestamps.Peek() < limiteInferior)
                {
                    _timestamps.Dequeue();
                }
                return _timestamps.Count == 0;
            }
        }
    }

    /// <summary>
    /// Tenta registrar uma nova requisição. Retorna false se o limite foi atingido.
    /// </summary>
    public bool TentarRegistrar(int limite, TimeSpan janela)
    {
        lock (_lock)
        {
            var agora = DateTime.UtcNow;
            var limiteInferior = agora.Subtract(janela);

            // Remove timestamps expirados
            while (_timestamps.Count > 0 && _timestamps.Peek() < limiteInferior)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count >= limite)
            {
                return false;
            }

            _timestamps.Enqueue(agora);
            return true;
        }
    }
}

/// <summary>
/// Extension method para registrar o middleware no pipeline.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
