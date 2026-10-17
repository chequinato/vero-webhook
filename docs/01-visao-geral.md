# 01 — Visão geral

> **🧠 Pra gravar**
> - O Vero é um **porteiro de transações**: recebe por webhook e decide se é confiável, suspeita ou bloqueada.
> - **Barato na hora, caro depois**: regras rápidas respondem na hora (403 ou 202); regras pesadas rodam em background.
> - **202 ≠ 200**: 202 é "recebi, ainda estou analisando".
> - **Quem decide é o modelo, não a pessoa**: o analista só pede a reavaliação.

## O que é

O Vero é um **sistema de detecção de fraudes em tempo real** para transações
financeiras. Ele funciona como um webhook: outro sistema envia cada transação
por HTTP POST, e o Vero valida, analisa e classifica.

## O caminho de uma transação

```
POST /transactions
      │
Rate limiting ── demais? → 429
      │
Autenticação HMAC ── assinatura inválida? → 401
      │
TransactionsController ── só converte o JSON e chama o service
      │
TransacaoService
  ├─ resolve remetente e destinatário ── conta não existe? → 400
  ├─ checa duplicata ── id repetido? → 409
  ├─ calcula o risco (velocity + modelo de ML)
  ├─ regras síncronas (valor alto, score baixo)
  │     ├─ violou → Bloqueada → 403
  │     └─ passou → AceitaProvisoria → 202 + vai para a fila
  └─ avisa o dashboard (SignalR)
      │
Worker (lê a fila)
  └─ regras assíncronas (velocity, horário estranho, valor redondo)
        ├─ anomalia → Suspeita + alerta
        └─ nada     → Aprovada
```

### Por que dividir em síncrono e assíncrono?

Quem mandou a transação não pode ficar esperando. O que é **barato** (comparar
um valor, olhar o score de uma conta) roda na hora. O que é **caro** (contar o
histórico do remetente) roda depois, sem segurar a resposta.

### Por que 202 e não 200?

`200 OK` diz "está feito". `202 Accepted` diz "recebi, mas ainda estou
processando". A transação aceita provisoriamente **ainda pode virar suspeita**,
então 202 é a resposta honesta. Status code carrega significado.

## A reavaliação

Transações em `Suspeita` ou `AceitaProvisoria` ficam numa fila de revisão. O
painel tem um botão que pede ao modelo uma nova decisão:

```
POST /api/dashboard/transactions/{id}/reavaliar
      │
ReavaliacaoService
  ├─ transação não existe? → 404
  ├─ já decidida (aprovada/bloqueada)? → 409
  ├─ recalcula as features na janela original da transação
  ├─ modelo devolve novo score
  ├─ PoliticaDeRisco: score ≥ 0,70 → Bloqueada · abaixo → Aprovada
  ├─ grava status + score numa transação de banco só
  └─ avisa o dashboard
```

Não existe decisão manual: o operador **dispara** a avaliação, quem **decide**
é o modelo. Isso impede que alguém libere uma fraude "na mão".

## Os quatro status

| Status | Quando |
|---|---|
| `AceitaProvisoria` | Passou nas regras síncronas, esperando o Worker |
| `Bloqueada` | Violou uma regra síncrona, ou o modelo bloqueou na reavaliação |
| `Suspeita` | O Worker encontrou uma anomalia |
| `Aprovada` | Passou em tudo |

## As pastas

| Pasta | O que é |
|---|---|
| `src/Vero.Domain/` | Entidades, enums, regras, políticas e interfaces. Não depende de ninguém |
| `src/Vero.Application/` | Services que orquestram o fluxo. Depende só do Domain |
| `src/Vero.Infrastructure/` | Banco, fila, alertas, criptografia, AWS |
| `src/Vero.ML/` | Modelo de Machine Learning |
| `src/Vero.Api/` | Controllers, middlewares, SignalR, `Program.cs` |
| `src/Vero.Worker/` | Consome a fila (BackgroundService ou AWS Lambda) |
| `simulator/` | Console que dispara transações falsas |
| `tests/` | 113 testes (xUnit + Moq) |
| `dashboard/` | Painel React com cena 3D e gráficos ao vivo |
| `infra/terraform/` | Infraestrutura como código |

---

**Próximo:** [02 — C#, a linguagem →](02-csharp.md)
