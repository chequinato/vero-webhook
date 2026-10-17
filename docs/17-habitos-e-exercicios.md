# 17 — Hábitos e exercícios

> **🧠 Pra gravar**
> - Antes de codar: **em que camada isso mora?**
> - Controller com `if` de negócio, classe com muitas dependências, `new` de infraestrutura, número mágico, copia-e-cola: são os cinco cheiros mais comuns.
> - Teste difícil = design pedindo para ser separado.

## Checklist de hábitos (antes de abrir um PR)

1. **Em que camada isso mora?** Regra de negócio → Domain. Orquestração →
   Application. Ferramenta → Infrastructure. HTTP → Api.
2. **Meu controller tem `if` de negócio?** Mova para a Application ou o Domain.
3. **Minha classe recebe mais de ~5 dependências?** Provavelmente faz coisa
   demais (SRP).
4. **Dei `new` em algo de infraestrutura dentro de um service?** Troque por uma
   interface injetada (DIP).
5. **Escrevi um número mágico** (`5`, `0.70f`)? Dê nome e deixe no lugar "dono"
   da regra.
6. **Copiei e colei um bloco?** Extraia (DRY). O `AddMinutes(-5)` repetido em
   dois controllers é o exemplo clássico deste projeto.
7. **Coloquei um segredo no código ou no `appsettings`?** Variável de ambiente,
   sem valor padrão.
8. **Registrei a peça nova no `DependencyInjection.cs` da camada certa?** O
   `CompositionRootTests` avisa se esqueceu.
9. **Escrevi o teste?**

## Exercícios

Para cada um, antes de codar, escreva: **qual problema? qual entrada? qual
saída? quais passos? dá para quebrar em partes menores?** As dicas estão
escondidas — abra uma de cada vez, só se travar.

### Exercício 1 — Nova regra síncrona (Strategy + Open/Closed)

Bloquear transações cuja `Moeda` não seja `"BRL"`.

<details><summary>Dica 1</summary>

Olhe a `ValorAltoRule`. A sua é muito parecida.
</details>

<details><summary>Dica 2</summary>

Ela implementa `IRegraSincrona`, mora em `src/Vero.Domain/Rules/Sync/` e
precisa de **uma** linha em `src/Vero.Application/DependencyInjection.cs`.
</details>

<details><summary>Dica 3</summary>

Quantos arquivos **existentes** você alterou além do `DependencyInjection.cs`?
Se a resposta for zero, você viu o Open/Closed funcionando. Agora escreva o
teste em `tests/Vero.Tests/Domain/`.
</details>

### Exercício 2 — Controlar o tempo nos testes (Dependency Inversion)

`TransacaoService` usa `DateTime.UtcNow` direto. Como testar que a janela de
velocity termina "agora", se "agora" muda a cada execução?

<details><summary>Dica 1</summary>

`DateTime.UtcNow` é uma dependência escondida — igual a dar `new` num repositório.
</details>

<details><summary>Dica 2</summary>

O .NET já tem a abstração: `TimeProvider`. Injete no construtor e use
`_timeProvider.GetUtcNow()`.
</details>

<details><summary>Dica 3</summary>

Em produção registre `TimeProvider.System`. No teste, use `FakeTimeProvider`
(pacote `Microsoft.Extensions.TimeProvider.Testing`) com uma data fixa.
</details>

### Exercício 3 — Separar leitura de escrita (Interface Segregation)

As rotas de leitura do `DashboardController` só consultam, mas dependem do
`ITransacaoRepository` inteiro (que também grava).

<details><summary>Dica 1</summary>

Liste quais métodos do repositório cada classe usa. Os grupos que aparecerem
são suas interfaces novas.
</details>

<details><summary>Dica 2</summary>

Algo como `IDashboardQueries` com `ObterEstatisticasAsync`,
`ListarPaginadoAsync` e `ObterVolumePorHoraAsync`. A mesma classe
`TransacaoRepository` pode implementar as duas interfaces.
</details>

### Exercício 4 — Escolher a implementação pela configuração (Factory)

Em produção o alerta deveria ir pelo SNS; em dev, pelo console. Hoje está fixo
no console.

<details><summary>Dica 1</summary>

O lugar é `src/Vero.Infrastructure/DependencyInjection.cs`. Nenhum service deve
saber qual implementação está rodando.
</details>

<details><summary>Dica 2</summary>

`services.AddSingleton<IAlertaService>(sp => ...)` aceita uma função que
**fabrica** a instância. Dentro dela, olhe uma variável de ambiente e devolva
uma ou outra.
</details>

### Exercício 5 — Log em volta de qualquer alerta (Decorator)

Registrar quanto tempo cada alerta demorou, seja console ou SNS, **sem
alterar** nenhuma das duas classes.

<details><summary>Dica 1</summary>

Crie uma classe que implementa `IAlertaService` **e recebe** outro
`IAlertaService` no construtor.
</details>

<details><summary>Dica 2</summary>

No `EnviarAlertaAsync`, inicie um `Stopwatch`, chame o alerta "de dentro" e
logue o tempo. Mesma interface por fora, comportamento extra em volta: isso é o
Decorator.
</details>

### Exercício 6 — Alinhar a infraestrutura

Corrija a inconsistência da armadilha 15: o Terraform declara `dotnet8`.

<details><summary>Dica</summary>

Veja quais runtimes gerenciados a Lambda aceita hoje. Se .NET 10 não estiver
disponível como runtime gerenciado, a alternativa é publicar como imagem de
contêiner ou com runtime customizado (`provided.al2023`).
</details>

---

**Anterior:** [← 16 — Armadilhas](16-armadilhas.md) · **Índice:** [Guia de estudo](README.md) · **Glossário:** [termos em uma linha](glossario.md)
