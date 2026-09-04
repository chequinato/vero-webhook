using Vero.Domain.Entities;
using Vero.Domain.Rules.Sync;

namespace Vero.Tests.Domain;

public class SyncRulesTests
{
    [Fact]
    public async Task ValorAltoRule_DeveBloquear_QuandoValorAcimaDoLimite()
    {
        var rule = new ValorAltoRule();
        var transacao = new Transacao { Id = "txn_001", Valor = 150_000m };

        var resultado = await rule.Avaliar(transacao);

        Assert.True(resultado);
    }

    [Fact]
    public async Task ValorAltoRule_NaoDeveBloquear_QuandoValorAbaixoDoLimite()
    {
        var rule = new ValorAltoRule();
        var transacao = new Transacao { Id = "txn_002", Valor = 50_000m };

        var resultado = await rule.Avaliar(transacao);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValorAltoRule_NaoDeveBloquear_QuandoValorExatamenteNoLimite()
    {
        var rule = new ValorAltoRule();
        var transacao = new Transacao { Id = "txn_003", Valor = ValorAltoRule.LimiteValor };

        var resultado = await rule.Avaliar(transacao);

        Assert.False(resultado);
    }
}
