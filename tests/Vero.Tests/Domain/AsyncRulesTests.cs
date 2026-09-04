using Vero.Domain.Entities;
using Vero.Domain.Rules.Async;

namespace Vero.Tests.Domain;

public class AsyncRulesTests
{
    [Theory]
    [InlineData(2, true)]   // 2h → horário estranho
    [InlineData(3, true)]   // 3h → horário estranho
    [InlineData(4, true)]   // 4h → horário estranho
    [InlineData(0, false)]  // meia-noite → fora da faixa
    [InlineData(5, false)]  // 5h → fora da faixa
    [InlineData(14, false)] // 14h → normal
    public async Task HorarioEstranhoRule_DeveAvaliarCorretamente(int hora, bool esperado)
    {
        var rule = new HorarioEstranhoRule();
        var transacao = new Transacao
        {
            Id = "txn_001",
            Timestamp = new DateTime(2026, 8, 10, hora, 0, 0, DateTimeKind.Utc)
        };

        var resultado = await rule.Avaliar(transacao);

        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData(1000, true)]    // R$1.000 → redondo e acima do mínimo
    [InlineData(5000, true)]    // R$5.000 → redondo e acima do mínimo
    [InlineData(10000, true)]   // R$10.000 → redondo
    [InlineData(999, false)]    // R$999 → abaixo do mínimo
    [InlineData(1500, false)]   // R$1.500 → não é múltiplo de 1000
    [InlineData(1001, false)]   // R$1.001 → não é múltiplo de 1000
    public async Task ValorRedondoRule_DeveAvaliarCorretamente(decimal valor, bool esperado)
    {
        var rule = new ValorRedondoRule();
        var transacao = new Transacao { Id = "txn_001", Valor = valor };

        var resultado = await rule.Avaliar(transacao);

        Assert.Equal(esperado, resultado);
    }
}
