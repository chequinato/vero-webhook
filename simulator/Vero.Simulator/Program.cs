using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// ─────────────────────────────────────────────────────────────
// Vero Simulator — gera transações fake para testar o sistema
//
// Modos de uso:
//   dotnet run                          → cenários fixos (7 transações)
//   dotnet run -- --continuous          → modo contínuo (transações infinitas)
//   dotnet run -- --continuous --tps 2  → 2 transações por segundo
//   dotnet run -- --url http://host:port
// ─────────────────────────────────────────────────────────────

var baseUrl = GetArg(args, "--url") ?? "http://localhost:5000";
var isContinuous = args.Contains("--continuous");
var tps = double.TryParse(GetArg(args, "--tps"), out var t) ? t : 1.0;

var hmacSecret = Environment.GetEnvironmentVariable("VERO_HMAC_SECRET")
    ?? "vero-dev-secret-nao-usar-em-producao";

using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

Console.WriteLine("🚀 Vero Simulator");
Console.WriteLine($"   URL: {baseUrl}");
Console.WriteLine($"   Modo: {(isContinuous ? $"contínuo ({tps} tx/s)" : "cenários fixos")}");
Console.WriteLine("───────────────────────────────────────────────");

// Verificar healthcheck
Console.Write("Verificando healthcheck... ");
try
{
    var health = await client.GetAsync("/health");
    if (health.IsSuccessStatusCode)
        Console.WriteLine("✅ API online!");
    else
    {
        Console.WriteLine($"❌ API retornou {health.StatusCode}");
        return;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Não foi possível conectar: {ex.Message}");
    return;
}

if (isContinuous)
    await RunContinuous(client, hmacSecret, tps);
else
    await RunFixedScenarios(client, hmacSecret);

// ─────────────────────────────────────────────────────────────
// Modo contínuo: gera transações aleatórias indefinidamente
// ─────────────────────────────────────────────────────────────
static async Task RunContinuous(HttpClient client, string hmacSecret, double tps)
{
    Console.WriteLine("\n📡 Modo contínuo — Ctrl+C para parar\n");

    var random = new Random();
    var contas = new[] { "conta_123", "conta_456", "conta_789", "conta_000", "conta_111", "conta_222" };
    var delayMs = (int)(1000 / tps);
    var seq = 0;
    var stats = new { enviadas = 0, aceitas = 0, bloqueadas = 0, erros = 0 };
    int enviadas = 0, aceitas = 0, bloqueadas = 0, erros = 0;

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Console.WriteLine($"\n\n📊 Resumo: {enviadas} enviadas, {aceitas} aceitas, {bloqueadas} bloqueadas, {erros} erros");
    };

    while (true)
    {
        seq++;
        var txId = $"txn_sim_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{seq:D4}";

        // Selecionar remetente e destinatário
        var remetenteIdx = random.Next(contas.Length);
        var destinatarioIdx = (remetenteIdx + 1 + random.Next(contas.Length - 1)) % contas.Length;

        // 85% transações normais, 15% padrões suspeitos
        decimal valor;
        DateTime timestamp;

        if (random.NextDouble() < 0.85)
        {
            // Normal: R$10 - R$5.000, horário atual
            valor = Math.Round((decimal)(random.NextDouble() * 4990 + 10), 2);
            timestamp = DateTime.UtcNow;
        }
        else
        {
            // Suspeita: escolher um padrão
            var padrao = random.Next(0, 4);
            valor = padrao switch
            {
                0 => Math.Round((decimal)(random.NextDouble() * 400_000 + 100_001), 2),  // Valor alto
                1 => random.Next(1, 50) * 1000m,                                          // Valor redondo
                2 => Math.Round((decimal)(random.NextDouble() * 20_000 + 500), 2),        // Normal mas horário estranho
                _ => Math.Round((decimal)(random.NextDouble() * 30_000 + 1000), 2),       // Velocity (burst)
            };

            // Horário estranho para alguns padrões
            timestamp = padrao == 2
                ? DateTime.UtcNow.Date.AddHours(random.Next(1, 6)).AddMinutes(random.Next(0, 60))
                : DateTime.UtcNow;
        }

        var tx = new
        {
            id = txId,
            valor,
            remetente = contas[remetenteIdx],
            destinatario = contas[destinatarioIdx],
            tipo = "pix",
            timestamp,
            moeda = "BRL"
        };

        try
        {
            var json = JsonSerializer.Serialize(tx, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var signature = CalcularHmac(json, hmacSecret);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Signature", signature);

            var response = await client.SendAsync(request);
            enviadas++;

            var statusIcon = (int)response.StatusCode switch
            {
                202 => "✅",
                403 => "🚫",
                409 => "♻️",
                _ => "❓"
            };

            if ((int)response.StatusCode == 202) aceitas++;
            else if ((int)response.StatusCode == 403) bloqueadas++;

            var body = await response.Content.ReadAsStringAsync();

            // Extrair risk score da resposta
            var riskInfo = "";
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("riskScore", out var rs) && rs.ValueKind == JsonValueKind.Number)
                {
                    riskInfo = $" risk:{rs.GetDouble() * 100:F0}%";
                }
            }
            catch { }

            Console.WriteLine($"  {statusIcon} [{seq:D4}] {txId}  R${valor,12:N2}  {(int)response.StatusCode}{riskInfo}  ({contas[remetenteIdx]} → {contas[destinatarioIdx]})");
        }
        catch (Exception ex)
        {
            erros++;
            Console.WriteLine($"  ❌ [{seq:D4}] {txId}  Erro: {ex.Message}");
        }

        await Task.Delay(delayMs);
    }
}

// ─────────────────────────────────────────────────────────────
// Modo fixo: cenários de teste predefinidos
// ─────────────────────────────────────────────────────────────
static async Task RunFixedScenarios(HttpClient client, string hmacSecret)
{
    var transacoes = new[]
    {
        new { id = "txn_001", valor = 1500.00m,    remetente = "conta_123", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },
        new { id = "txn_002", valor = 150000.00m,  remetente = "conta_123", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // valor_alto → bloqueio
        new { id = "txn_003", valor = 500.00m,     remetente = "conta_789", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // score_baixo → bloqueio
        new { id = "txn_004", valor = 5000.00m,    remetente = "conta_000", destinatario = "conta_123", tipo = "pix", timestamp = DateTime.UtcNow.AddHours(-21), moeda = "BRL" },  // horário 3h → suspeita assíncrona
        new { id = "txn_005", valor = 10000.00m,   remetente = "conta_456", destinatario = "conta_123", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // valor_redondo → suspeita assíncrona
        new { id = "txn_006", valor = 250.50m,     remetente = "conta_456", destinatario = "conta_000", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // normal → aprovada
        new { id = "txn_001", valor = 1500.00m,    remetente = "conta_123", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // replay → 409 Conflict
    };

    var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    foreach (var tx in transacoes)
    {
        Console.WriteLine($"\n📤 Enviando transação {tx.id} (R${tx.valor:N2})...");

        try
        {
            var json = JsonSerializer.Serialize(tx, jsonOptions);
            var signature = CalcularHmac(json, hmacSecret);

            var request = new HttpRequestMessage(HttpMethod.Post, "/transactions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Signature", signature);

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"   Status HTTP: {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"   Resposta: {body}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro: {ex.Message}");
        }

        await Task.Delay(500);
    }

    Console.WriteLine("\n───────────────────────────────────────────────");
    Console.WriteLine("✅ Simulação concluída!");
}

/// <summary>
/// Calcula a assinatura HMAC-SHA256 do payload.
/// </summary>
static string CalcularHmac(string payload, string secret)
{
    var keyBytes = Encoding.UTF8.GetBytes(secret);
    var payloadBytes = Encoding.UTF8.GetBytes(payload);

    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(payloadBytes);
    return Convert.ToHexStringLower(hash);
}

/// <summary>
/// Extrai o valor de um argumento "--key value" dos args.
/// </summary>
static string? GetArg(string[] args, string key)
{
    var idx = Array.IndexOf(args, key);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}
