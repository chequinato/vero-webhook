# 📚 Guia de Estudo — Vero

Material para entender cada parte deste projeto e, de quebra, aprender **C#**,
**orientação a objetos e SOLID**, **Clean Architecture**, **design patterns**,
**Machine Learning**, **React em tempo real**, **WebGL**, **AWS** e
**segurança de APIs**.

Cada arquivo trata de **um assunto** e começa com um quadro **"Pra gravar"**:
o resumo em poucas linhas. Leia o quadro primeiro, depois o resto. No dia
seguinte, releia só os quadros — é a revisão que fixa.

## Ordem sugerida

### Trilha 1 — o essencial (backend)

| # | Arquivo | Assunto |
|---|---|---|
| 01 | [Visão geral](01-visao-geral.md) | O que o Vero faz e o caminho de uma transação |
| 02 | [C# — a linguagem](02-csharp.md) | Só os recursos que aparecem no projeto |
| 03 | [Clean Architecture](03-clean-architecture.md) | Camadas, dependências e injeção de dependência |
| 04 | [SOLID e orientação a objetos](04-solid-e-poo.md) | Os cinco princípios com o código do Vero |
| 05 | [Design patterns](05-design-patterns.md) | Strategy, Repository, Adapter, Observer, Result… |
| 06 | [As camadas na prática](06-camadas.md) | Domain, Application, Infrastructure e API |
| 07 | [Banco de dados com EF Core](07-banco-de-dados.md) | DbContext, migrations, criptografia transparente |
| 08 | [Segurança](08-seguranca.md) | HMAC, AES, rate limiting e segredos fora do código |
| 09 | [Machine Learning com ML.NET](09-machine-learning.md) | Features, FastTree e predição |
| 10 | [Testes](10-testes.md) | xUnit, Moq e o que cada pasta cobre |

### Trilha 2 — front-end e tempo real

| # | Arquivo | Assunto |
|---|---|---|
| 11 | [Dashboard React](11-dashboard-react.md) | Stack, hooks, cliente HTTP tipado, animação |
| 12 | [Estado ao vivo no cliente](12-estado-ao-vivo.md) | Atualização otimista, imutabilidade, idempotência |
| 13 | [Visualização 3D com WebGL](13-webgl.md) | Cena, shaders, pool de objetos, desempenho |

### Trilha 3 — operação e prática

| # | Arquivo | Assunto |
|---|---|---|
| 14 | [Infraestrutura AWS](14-aws.md) | SQS, SNS, Lambda e Terraform |
| 15 | [Como rodar](15-como-rodar.md) | Comandos e variáveis de ambiente |
| 16 | [Armadilhas encontradas de verdade](16-armadilhas.md) | Bugs reais: sintoma, causa e lição |
| 17 | [Hábitos e exercícios](17-habitos-e-exercicios.md) | Checklist antes do PR e exercícios com dicas |
| — | [Glossário](glossario.md) | Os termos em uma linha |

## Três arquivos que valem mais que o resto

- **[04 — SOLID](04-solid-e-poo.md)**: os princípios com o "antes e depois"
  da refatoração deste repositório, incluindo onde o projeto *não* os segue.
- **[12 — Estado ao vivo](12-estado-ao-vivo.md)**: padrões que servem para
  qualquer aplicação que receba eventos.
- **[16 — Armadilhas](16-armadilhas.md)**: bugs que aconteceram de verdade,
  com a lição de cada um.
