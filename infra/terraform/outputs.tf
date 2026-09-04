output "sqs_queue_url" {
  description = "URL da fila SQS para análise assíncrona"
  value       = aws_sqs_queue.vero_analise.url
}

output "sns_topic_arn" {
  description = "ARN do tópico SNS para alertas"
  value       = aws_sns_topic.vero_alertas.arn
}

output "lambda_function_name" {
  description = "Nome da função Lambda worker"
  value       = aws_lambda_function.vero_worker.function_name
}

output "lambda_function_arn" {
  description = "ARN da função Lambda worker"
  value       = aws_lambda_function.vero_worker.arn
}
