# Lambda para processamento assíncrono de transações
# Always Free: 1 milhão de invocações/mês + 400.000 GB-segundos grátis (para sempre)
#
# Trigger: SQS → Lambda → analisa transação → atualiza DB → SNS (se anomalia)

# IAM Role da Lambda
resource "aws_iam_role" "vero_lambda" {
  name = "${var.project_name}-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = {
    Project = var.project_name
  }
}

# Permissões básicas de execução (CloudWatch Logs)
resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.vero_lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# Permissão para consumir da SQS
resource "aws_iam_role_policy" "lambda_sqs" {
  name = "${var.project_name}-lambda-sqs"
  role = aws_iam_role.vero_lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes"
        ]
        Resource = aws_sqs_queue.vero_analise.arn
      }
    ]
  })
}

# Permissão para publicar no SNS
resource "aws_iam_role_policy" "lambda_sns" {
  name = "${var.project_name}-lambda-sns"
  role = aws_iam_role.vero_lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sns:Publish"
        ]
        Resource = aws_sns_topic.vero_alertas.arn
      }
    ]
  })
}

# Função Lambda
resource "aws_lambda_function" "vero_worker" {
  function_name = "${var.project_name}-worker"
  role          = aws_iam_role.vero_lambda.arn
  handler       = "Vero.Worker::Vero.Worker.LambdaHandler::HandleAsync"
  runtime       = "dotnet8"
  timeout       = 30
  memory_size   = 256

  # O arquivo ZIP do deploy é gerado pelo CI/CD — placeholder para terraform plan
  filename         = "lambda_placeholder.zip"
  source_code_hash = filebase64sha256("lambda_placeholder.zip")

  environment {
    variables = {
      VERO_CONNECTION_STRING = var.db_connection_string
      VERO_SNS_TOPIC_ARN    = aws_sns_topic.vero_alertas.arn
    }
  }

  tags = {
    Project = var.project_name
  }

  lifecycle {
    # O deploy do código é feito via CI/CD, não pelo Terraform
    ignore_changes = [filename, source_code_hash]
  }
}

# Trigger: SQS → Lambda (batch de 1 mensagem por vez)
resource "aws_lambda_event_source_mapping" "sqs_trigger" {
  event_source_arn = aws_sqs_queue.vero_analise.arn
  function_name    = aws_lambda_function.vero_worker.arn
  batch_size       = 1
  enabled          = true
}
