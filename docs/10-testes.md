# 10 — Testes

> **🧠 Pra gravar**
> - Todo teste tem três partes: **Arrange** (prepara), **Act** (executa), **Assert** (confere).
> - `[Fact]` = um caso · `[Theory]` + `[InlineData]` = o mesmo teste com vários dados.
> - **Mock** finge ser uma dependência: você diz o que ela devolve e depois confere como foi chamada.
> - Teste difícil de escrever é recado do design: a classe faz coisa demais.

## xUnit

```csharp
[Fact]
public async Task ProcessarTransacao_RegraViolada_DeveBloquear()
{
    // Arrange
    var service = CriarService([CriarRegra("regra_teste", violada: true).Object]);

    // Act
    var resultado = await service.ProcessarTransacaoAsync(CriarNovaTransacao());

    // Assert
    Assert.Equal(DesfechoRecebimento.Bloqueada, resultado.Desfecho);
    Assert.Equal("regra_teste", resultado.Transacao!.Motivo);
}
```

**Nome do teste** = `Metodo_Cenario_ResultadoEsperado`. Quando falha, o nome já
diz o que quebrou.

```csharp
[Theory]
[InlineData(StatusTransacao.Suspeita, true)]
[InlineData(StatusTransacao.AceitaProvisoria, true)]
[InlineData(StatusTransacao.Aprovada, false)]
[InlineData(StatusTransacao.Bloqueada, false)]
public void PodeSerReavaliada_SoStatusEmRevisao(StatusTransacao status, bool esperado)
{
    Assert.Equal(esperado, PoliticaDeRisco.PodeSerReavaliada(status));
}
```

## Moq

```csharp
var repo = new Mock<ITransacaoRepository>();

// "Quando chamarem isto, devolva aquilo"
repo.Setup(r => r.ObterPorIdAsync("txn_001")).ReturnsAsync(transacaoExistente);

var service = new TransacaoService(repo.Object, /* ... */);
await service.ProcessarTransacaoAsync(nova);

// "Confira que isto foi chamado (ou não)"
repo.Verify(r => r.AdicionarAsync(It.IsAny<Transacao>()), Times.Never);
```

- `It.IsAny<T>()` aceita qualquer valor.
- `It.Is<T>(t => ...)` aceita só o que passa na condição.
- `Times.Once`, `Times.Never` dizem quantas vezes.

Mocks só funcionam porque os services dependem de **interfaces** — é a
inversão de dependência pagando a conta (veja [04 — SOLID](04-solid-e-poo.md)).

## O que cada pasta cobre (113 testes)

| Pasta | Testes | O que cobre |
|---|---|---|
| `Domain/` | 32 | As 5 regras e a `PoliticaDeRisco` — **sem mock nenhum** |
| `Application/` | 30 | `TransacaoService`, `ReavaliacaoService`, `AnomalyDetectionService` |
| `Security/` | 17 | AES, HMAC, rate limiting |
| `ML/` | 14 | Gerador de dados, treino (AUC, accuracy), predição |
| `Api/` | 9 | `TransactionsController`: desfecho → status HTTP |
| `Infrastructure/` | 7 | `TransacaoRepository` com EF Core InMemory |
| `CompositionRootTests` | 4 | Os `Add*` de cada camada montam o container; sem configuração a infra recusa subir |

## Três lições dos testes deste projeto

**1. Regra pura é o teste mais barato.** Compare `PoliticaDeRiscoTests` (sem
mock, uma linha por caso) com o antigo teste do controller (cinco mocks). Só
ficou barato porque a decisão saiu do controller e foi para o Domain.

**2. Teste a montagem, não só as peças.** `CompositionRootTests` constrói o
container de DI com `ValidateOnBuild = true`. Se alguém criar um service novo e
esquecer de registrar uma dependência, quebra aqui — e não em produção.

**3. Verifique sem a ferramenta suspeita.** A cena 3D do painel não cabe em
xUnit, e o navegador usado para inspecioná-la se mostrou pouco confiável. A
saída foi rodar a lógica de roteamento **em Node**, fora do navegador: 400
transações a 60 Hz, conferindo que todo trecho pertence ao estágio certo, que o
roteamento é determinístico, que nenhuma posição sai não-finita e que toda
partícula pousa no portão do status real. Só foi possível porque
`engine/topologia.ts` não depende de `three` nem do DOM. **Separe a lógica pura
da apresentação e ela fica testável sem a apresentação.**

## Rodar

```bash
dotnet test Vero.slnx
```

---

**Anterior:** [← 09 — Machine Learning](09-machine-learning.md) · **Próximo:** [11 — Dashboard React →](11-dashboard-react.md)
