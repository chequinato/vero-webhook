# Fila SQS para análise assíncrona de transações
# Always Free: 1 milhão de requisições/mês grátis (para sempre)

resource "aws_sqs_queue" "vero_analise" {
  name                       = "${var.project_name}-analise-queue"
  delay_seconds              = 0
  max_message_size           = 262144 # 256 KB
  message_retention_seconds  = 345600 # 4 dias
  visibility_timeout_seconds = 60
  receive_wait_time_seconds  = 20 # Long polling

  tags = {
    Project = var.project_name
  }
}

# Dead Letter Queue — mensagens que falharam após várias tentativas
resource "aws_sqs_queue" "vero_analise_dlq" {
  name                      = "${var.project_name}-analise-dlq"
  message_retention_seconds = 1209600 # 14 dias

  tags = {
    Project = var.project_name
  }
}

resource "aws_sqs_queue_redrive_policy" "vero_analise_redrive" {
  queue_url = aws_sqs_queue.vero_analise.id

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.vero_analise_dlq.arn
    maxReceiveCount     = 3
  })
}
