# Vero — API de Webhooks com Detecção de Anomalias em Tempo Real

API que recebe transações financeiras via webhook e decide, em tempo real, se
a transação é confiável, suspeita ou deve ser bloqueada imediatamente.

## Modelo híbrido de detecção

- **Síncrono**: Casos óbvios de fraude são bloqueados na hora (resposta 403).
- **Assíncrono**: Casos duvidosos são aceitos provisoriamente (resposta 202) e
  analisados em background por um worker. Se uma anomalia for confirmada, o
  status é atualizado e um alerta é disparado.

## Stack

| Camada | Tecnologia |
|--------|------------|
| Linguagem | C# (.NET) |
| Framework | ASP.NET Core |
| Banco de dados | PostgreSQL |
| ORM | Entity Framework Core |
| Fila | AWS SQS (ou simulação em memória) |
| Alertas | AWS SNS (ou console log local) |
| Testes | xUnit |
| Infra | Terraform |

## Estrutura do projeto

```
Vero/
├── src/
│   ├── Vero.Api/              # Controllers, DTOs, Middleware
│   ├── Vero.Domain/           # Entidades, Enums, Regras, Interfaces
│   ├── Vero.Application/      # Services (orquestração)
│   ├── Vero.Infrastructure/   # Repositórios, DbContext, AWS clients
│   └── Vero.Worker/           # Worker que consome a fila
├── simulator/
│   └── Vero.Simulator/        # Dispara transações fake
├── tests/
│   └── Vero.Tests/            # Testes unitários (xUnit)
└── infra/
    └── terraform/             # Infraestrutura como código
```

## Como rodar localmente

### Pré-requisitos

- .NET 8+ SDK
- PostgreSQL rodando em `localhost:5432`

### Setup

1. Clone o repositório:
   ```bash
   git clone https://github.com/chequinato/vero-c-.git
   cd vero-c-
   ```

2. Configure a connection string (copie `.env.example` para `.env` e ajuste):
   ```bash
   cp .env.example .env
   ```

3. Aplique as migrations:
   ```bash
   dotnet ef database update --project src/Vero.Infrastructure --startup-project src/Vero.Api
   ```

4. Rode a API:
   ```bash
   dotnet run --project src/Vero.Api
   ```

## Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| `POST` | `/transactions` | Recebe uma transação via webhook |
| `GET` | `/transactions/{id}` | Consulta status de uma transação |
| `GET` | `/transactions?suspicious=true` | Lista transações suspeitas |
| `GET` | `/health` | Healthcheck |

## Regras de anomalia

### Síncronas (bloqueiam na hora)
- Valor acima de R$100.000
- Score de confiança da conta abaixo de 35

### Assíncronas (análise em background)
- Velocity: múltiplas transações do mesmo remetente em 5 minutos
- Horário estranho: transações entre 1h e 5h
- Valor redondo: múltiplos de R$1.000 acima de R$1.000
