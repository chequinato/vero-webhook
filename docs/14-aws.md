# 14 — Infraestrutura AWS

> **🧠 Pra gravar**
> - **SQS** = fila · **SNS** = notificação pub/sub · **Lambda** = função que roda sob demanda · **RDS** = banco gerenciado.
> - **DLQ** recebe a mensagem que falhou 3 vezes — nada se perde em silêncio.
> - **Terraform** descreve a infraestrutura em código; sempre `plan` antes de `apply`.
> - O Worker roda igual como `BackgroundService` (local) ou Lambda (AWS).

## Arquitetura na nuvem

```
            Internet
               │
        API Gateway / ALB
               │
         ┌─────┴─────┐
         │  Vero.Api  │
         └─────┬─────┘
        ┌──────┼──────┐
   PostgreSQL  SQS   SignalR
   (RDS)       │
         ┌─────┴─────┐
         │  Lambda    │  (Vero.Worker)
         └─────┬─────┘
         ┌─────┴─────┐
         │    SNS     │  → e-mail com o alerta
         └───────────┘
```

| Serviço | O que faz | Parecido com |
|---|---|---|
| **SQS** | Fila de mensagens | RabbitMQ |
| **SNS** | Pub/sub de notificações | Kafka, bem mais simples |
| **Lambda** | Função serverless, paga por execução | Azure Functions |
| **RDS** | Banco gerenciado | Heroku Postgres |

Tudo foi pensado para caber no **Free Tier** — menos o RDS, que existe só como
referência (`rds.tf`, marcado para **não** aplicar).

## Terraform

```hcl
# sqs.tf — fila principal com Dead Letter Queue
resource "aws_sqs_queue" "analise_queue" {
  message_retention_seconds  = 345600   # 4 dias
  visibility_timeout_seconds = 60       # some da fila enquanto alguém processa
  receive_wait_time_seconds  = 20       # long polling

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.analise_dlq.arn
    maxReceiveCount     = 3             # 3 falhas → DLQ
  })
}

# lambda.tf
resource "aws_lambda_function" "worker" {
  handler     = "Vero.Worker::Vero.Worker.LambdaHandler::HandleAsync"
  runtime     = "dotnet8"
  memory_size = 256
  timeout     = 30
}

resource "aws_lambda_event_source_mapping" "sqs_trigger" {
  batch_size = 1          # uma transação por invocação
}
```

- **Visibility timeout:** quando alguém pega a mensagem, ela fica invisível por
  60 s. Se o processamento não confirmar a tempo, ela volta para a fila.
- **Long polling:** em vez de perguntar "tem mensagem?" sem parar, espera até
  20 s por uma resposta. Menos requisições, menos custo.
- **DLQ (Dead Letter Queue):** a mensagem que falhou 3 vezes vai para uma fila
  separada, guardada por 14 dias para investigação.

> ⚠️ **Inconsistência conhecida:** o Terraform declara `runtime = "dotnet8"`,
> mas o projeto compila para **.NET 10**. Antes de fazer deploy de verdade,
> é preciso alinhar os dois (veja [16 — Armadilhas](16-armadilhas.md)).

```bash
cd infra/terraform
terraform init
terraform plan     # sempre olhe o que vai mudar
terraform apply
```

## Worker em dois modos

```csharp
// Local: BackgroundService que consulta a fila
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var transacaoId = await filaTransacao.ConsumirProximaAsync();
        if (transacaoId is not null)
        {
            using var scope = scopeFactory.CreateScope();   // services scoped precisam de um escopo
            var service = scope.ServiceProvider.GetRequiredService<AnomalyDetectionService>();
            await service.AnalisarAsync(transacaoId);
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);   // fila vazia: espera
        }
    }
}

// AWS: a Lambda é acionada pelo SQS com um lote de mensagens
public async Task HandleAsync(SQSEvent sqsEvent, ILambdaContext context)
{
    foreach (var record in sqsEvent.Records)
    {
        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AnomalyDetectionService>()
            .AnalisarAsync(record.Body);
        // exceção → relançada → o SQS reenfileira (e manda para a DLQ após 3 falhas)
    }
}
```

**Por que `CreateScope()`?** O `BackgroundService` é singleton, mas o
`AnomalyDetectionService` é scoped (usa `DbContext`). Cada mensagem ganha seu
próprio escopo, como se fosse uma requisição HTTP.

Os dois modos montam as dependências com o mesmo
`AddVeroInfrastructure()` + `AddVeroAnaliseAssincrona()`.

## O que ainda não está ligado

`SqsFilaTransacao` e `SnsAlertaService` existem e implementam as interfaces,
mas o registro padrão em `Infrastructure/DependencyInjection.cs` ainda usa as
versões locais (`InMemoryFilaTransacao`, `ConsoleAlertaService`). Escolher pela
configuração é o exercício 4 de [17 — Hábitos e exercícios](17-habitos-e-exercicios.md).

---

**Anterior:** [← 13 — WebGL](13-webgl.md) · **Próximo:** [15 — Como rodar →](15-como-rodar.md)
