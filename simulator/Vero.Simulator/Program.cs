using System.Net.Http.Json;
using System.Text.Json;

Console.WriteLine("🚀 Vero Simulator — Disparando transações fake");
Console.WriteLine("───────────────────────────────────────────────");

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5000";
using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

// Primeiro verifica se a API está de pé
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

// Transações de teste — cobrem todos os cenários
var transacoes = new[]
{
    new { id = "txn_001", valor = 1500.00m,    remetente = "conta_123", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },
    new { id = "txn_002", valor = 150000.00m,  remetente = "conta_123", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // valor_alto → bloqueio
    new { id = "txn_003", valor = 500.00m,     remetente = "conta_789", destinatario = "conta_456", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // score_baixo → bloqueio
    new { id = "txn_004", valor = 5000.00m,    remetente = "conta_000", destinatario = "conta_123", tipo = "pix", timestamp = DateTime.UtcNow.AddHours(-21), moeda = "BRL" },  // horário 3h → suspeita assíncrona
    new { id = "txn_005", valor = 10000.00m,   remetente = "conta_456", destinatario = "conta_123", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // valor_redondo → suspeita assíncrona
    new { id = "txn_006", valor = 250.50m,     remetente = "conta_456", destinatario = "conta_000", tipo = "pix", timestamp = DateTime.UtcNow, moeda = "BRL" },  // normal → aprovada
};

var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

foreach (var tx in transacoes)
{
    Console.WriteLine($"\n📤 Enviando transação {tx.id} (R${tx.valor:N2})...");

    try
    {
        var response = await client.PostAsJsonAsync("/transactions", tx);
        var body = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"   Status HTTP: {(int)response.StatusCode} {response.StatusCode}");
        Console.WriteLine($"   Resposta: {body}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ❌ Erro: {ex.Message}");
    }

    await Task.Delay(500); // Pequeno delay entre transações
}

Console.WriteLine("\n───────────────────────────────────────────────");
Console.WriteLine("✅ Simulação concluída!");
