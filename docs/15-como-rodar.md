# 15 — Como rodar

> **🧠 Pra gravar**
> - Modo rápido: `--dev` usa SQLite em memória e já vem com ~300 transações.
> - A API e o simulador precisam do **mesmo** `VERO_HMAC_SECRET`.
> - Porta da API: **5000** · dashboard: **5173**.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (para o dashboard)
- PostgreSQL — só para o modo completo

## Testes

```bash
dotnet test Vero.slnx
```

## Modo rápido (sem banco)

```bash
# Qualquer valor serve, desde que seja o MESMO na API e no simulador
export VERO_HMAC_SECRET=um-segredo-local-qualquer

# Terminal 1 — API em http://localhost:5000
dotnet run --project src/Vero.Api -- --dev

# Terminal 2 — simulador mandando transações sem parar (mesmo VERO_HMAC_SECRET)
dotnet run --project simulator/Vero.Simulator -- --continuous --tps 2

# Terminal 3 — dashboard em http://localhost:5173
cd dashboard && npm install && npm run dev
```

O `--dev` liga:

- SQLite em memória (sem PostgreSQL);
- sem criptografia em repouso;
- 6 contas e ~300 transações de exemplo;
- limite de 300 requisições por minuto (`appsettings.Development.json`).

Sem a API, o painel também abre em `http://localhost:5173/?demo`, com dados
sintéticos que se mexem.

## Simulador

```bash
# 7 cenários fixos: normal, valor alto, score baixo, horário, valor redondo, aprovada e replay
dotnet run --project simulator/Vero.Simulator

# Contínuo, 3 transações por segundo, outra URL
dotnet run --project simulator/Vero.Simulator -- --continuous --tps 3 --url http://localhost:5000
```

Ele assina cada requisição com HMAC. Com o segredo errado, a API responde
**401** — é um bom jeito de ver a segurança funcionando.

## Modo completo (PostgreSQL)

```bash
cp .env.example .env                       # preencha a senha do banco e as chaves
export $(grep -v '^#' .env | xargs)        # carrega as variáveis (em cada terminal)

dotnet ef database update --project src/Vero.Infrastructure --startup-project src/Vero.Api
dotnet run --project src/Vero.Api
dotnet run --project src/Vero.Worker
```

| Variável | Para quê | Obrigatória |
|---|---|---|
| `VERO_HMAC_SECRET` | Assinatura do webhook | Sempre |
| `VERO_CONNECTION_STRING` | Banco PostgreSQL | Modo completo e Worker |
| `VERO_ENCRYPTION_KEY` | Criptografia em repouso | Modo completo e Worker |

Se faltar alguma, a aplicação não sobe e diz qual é.

## Modelo de ML

Não precisa treinar à mão: se `Models/fraud_model.zip` não existir, a API
treina um modelo na primeira subida (alguns segundos a mais).

---

**Anterior:** [← 14 — AWS](14-aws.md) · **Próximo:** [16 — Armadilhas →](16-armadilhas.md)
