# 04 — SOLID e orientação a objetos

> **🧠 Pra gravar**
> - **S** — um motivo para mudar · **O** — estende sem editar · **L** — qualquer implementação entra no lugar da outra · **I** — ninguém depende do que não usa · **D** — todos dependem de abstração.
> - Composição ("usa um") > herança ("é um").
> - Princípio sem julgamento vira cerimônia: interface sem motivo é custo.

Clean Architecture diz **onde** cada coisa mora. SOLID diz **como** escrever as
classes que moram lá. Cada princípio resolve um problema concreto de manutenção.

## S — Single Responsibility (responsabilidade única)

> Uma classe deve ter um, e apenas um, motivo para mudar.

Não é "a classe faz uma coisa só". É sobre **motivos de mudança**: se duas
pessoas, por razões diferentes, pediriam alterações na mesma classe, ela tem
responsabilidade demais.

**Antes da refatoração**, o `TransactionsController` resolvia contas, contava
velocity, chamava o ML, chamava o service, montava o payload do SignalR e
escolhia o status HTTP. Seis motivos para mudar. O teste dele precisava de
**cinco mocks**.

**Depois**, cada motivo de mudança tem seu dono:

| Classe | Único motivo para mudar |
|---|---|
| `RateLimitingMiddleware` | Mudou o limite de requisições |
| `HmacAuthenticationMiddleware` | Mudou a política de autenticação |
| `TransactionsController` | Mudou o contrato HTTP (rota, status code, DTO) |
| `TransacaoService` | Mudou a **ordem** do fluxo |
| `CalculadoraDeRisco` | Mudou como o risco é obtido (janela de velocity + modelo) |
| `ValorAltoRule` | Mudou o limite de valor |
| `PoliticaDeRisco` | Mudou o corte de 70% do modelo |
| `TransacaoRepository` | Mudou o jeito de persistir |
| `MlRiskScoringService` | Mudou o modelo ou as features |

O teste do controller agora usa **um mock**. Testes difíceis de escrever
costumam ser sinal de classe com responsabilidade demais.

**Sinais de violação:** "e" na descrição ("valida **e** salva"), arquivo com
centenas de linhas, `using` misturando camadas (`System.Security.Cryptography`
junto com `Microsoft.EntityFrameworkCore`).

## O — Open/Closed (aberto/fechado)

> Aberto para extensão, fechado para modificação.

O consumidor das regras **não conhece nenhuma regra concreta**:

```csharp
// TransacaoService — recebe a coleção por DI
private readonly IEnumerable<IRegraSincrona> _regrasSincronas;

var regraViolada = await _regrasSincronas.PrimeiraVioladaAsync(transacao);
if (regraViolada is not null)
{
    transacao.Status = StatusTransacao.Bloqueada;
    transacao.Motivo = regraViolada.Nome;   // o nome vem da própria regra
}
```

Para criar uma regra de geolocalização:

1. criar `GeolocalizacaoRule : IRegraSincrona`;
2. adicionar uma linha em `src/Vero.Application/DependencyInjection.cs`.

O `TransacaoService` **não é tocado**, nem os testes dele. Repare que
`transacao.Motivo = regraViolada.Nome` é o que permite isso: um `switch`
traduzindo tipo de regra para mensagem obrigaria a editar o service a cada
regra nova.

## L — Liskov Substitution (substituição)

> Trocar uma implementação por outra não pode quebrar quem usa.

- `IFilaTransacao` tem `InMemoryFilaTransacao` (dev) e `SqsFilaTransacao` (produção). **As duas devolvem `null` quando a fila está vazia** — está escrito no contrato da interface. Se a do SQS lançasse exceção nesse caso, o Worker quebraria só em produção.
- `VeroDbContext` roda sobre PostgreSQL ou SQLite; o repositório é o mesmo código.
- Nos testes, `Mock<ITransacaoRepository>` substitui o repositório real sem o service perceber.

**Como se viola:** implementação que lança `NotImplementedException`, ou que
exige uma pré-condição a mais ("chame `Iniciar()` antes"). Aí o chamador
precisa de `if (fila is SqsFilaTransacao)` — e o polimorfismo morreu.

## I — Interface Segregation (segregação)

> Ninguém deve depender de métodos que não usa.

- **`IRegraSincrona` × `IRegraAssincrona`.** Antes toda regra era `IRule`, e
  quem decidia se ela era rápida ou pesada era a ordem de registro no
  `Program.cs`. A Api chegou a injetar regras **síncronas** no serviço de
  análise **assíncrona** sem ninguém perceber. Com uma interface por tipo, o
  **compilador** impede o erro.
- **`ITransacaoService` × `IReavaliacaoService`.** O `DashboardController` não
  precisa conhecer o recebimento de webhook.
- **`IRiskScoringService`** tem **um método só** — fácil de implementar, de
  simular e impossível de entender errado.

**Onde o projeto está no limite:** `ITransacaoRepository` tem **dez métodos** e
nenhum consumidor usa todos. O dashboard só lê; o `TransacaoService` escreve.
Separar em leitura e escrita seria a evolução natural (é o exercício 3 de
[17 — Hábitos e exercícios](17-habitos-e-exercicios.md)). Não foi feito porque
segregar cedo demais também custa: mais arquivos, mais registro, mais
indireção. O princípio indica a direção; o julgamento decide o momento.

## D — Dependency Inversion (inversão de dependência)

> Alto nível e baixo nível dependem de abstrações.

Repare **onde a interface mora**:

```
src/Vero.Domain/Interfaces/ITransacaoRepository.cs           ← o contrato
src/Vero.Infrastructure/Repositories/TransacaoRepository.cs  ← a implementação
```

O contrato fica com **quem usa**, não com quem implementa. Isso inverte a seta:

```
sem inversão:   Application ──────────────→ Infrastructure
com inversão:   Application ──→ IRepository ←── Infrastructure
                                (Domain)        (implementa)
```

Exemplo da refatoração: o `TransacaoService` precisa avisar o dashboard. Ele
não conhece SignalR — conhece `INotificadorTransacao`. Quem implementa é o
`TransactionHubNotifier`, na Api. No teste, vira um `Mock<INotificadorTransacao>`.
Trocar SignalR por WebSocket puro mexe em **um** arquivo.

## Os quatro pilares de orientação a objetos no projeto

**Abstração** — expor o *quê*, esconder o *como*. `IRiskScoringService` diz
"me dê um risco de 0 a 1" e nada revela que por trás existe ML.NET.

**Encapsulamento** — estado protegido por comportamento. O `PredictionEngine`
do ML.NET **não é thread-safe**, então o `MlRiskScoringService` o guarda como
`private readonly` e serializa o acesso:

```csharp
private readonly PredictionEngine<TransacaoFeatures, TransacaoPrediction> _predictionEngine;
private readonly object _predictionLock = new();

lock (_predictionLock)
{
    prediction = _predictionEngine.Predict(features);
}
```

Quem chama não precisa saber disso — e não tem como estragar.

**Herança** — quase não aparece, de propósito. A única real é `ControllerBase`,
imposta pelo framework.

**Polimorfismo** — a mesma chamada `regra.Avaliar(t)` executa código diferente
conforme o objeto concreto. Sem `switch`, sem `if (regra is ValorAltoRule)`.

## Composição sobre herança

```csharp
// ✗ Herança — amarra o filho ao pai, e só dá para herdar de um
public class TransacaoService : RepositorioBase { ... }

// ✓ Composição — recebe o que precisa, por contrato
public class TransacaoService(ITransacaoRepository repo, IFilaTransacao fila, ...) { ... }
```

Herde quando for genuinamente "**é um** tipo de". Componha quando for "**usa um**".

## Quando SOLID atrapalha

Sinais de excesso:

- interface com uma implementação só, criada "por precaução";
- camada que só repassa chamadas sem decidir nada;
- tantos arquivos pequenos que entender um fluxo exige abrir doze.

No Vero, `IRule` tem cinco implementações e `IFilaTransacao` tem duas — as
abstrações se pagaram. Já a `CalculadoraDeRisco` **não tem interface de
propósito**: não existe motivo real para ter duas versões, e ela já é testável
pelas dependências que recebe. As rotas de leitura do `DashboardController`
falam direto com o repositório pelo mesmo motivo: criar um service só para
repassar a chamada seria cerimônia (YAGNI).

---

**Anterior:** [← 03 — Clean Architecture](03-clean-architecture.md) · **Próximo:** [05 — Design patterns →](05-design-patterns.md)
