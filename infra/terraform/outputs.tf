output "sqs_queue_url" {
  description = "URL da fila SQS para análise assíncrona"
  value       = aws_sqs_queue.vero_analise.url
}

output "sns_topic_arn" {
  description = "ARN do tópico SNS para alertas"
  value       = aws_sns_topic.vero_alertas.arn
}
