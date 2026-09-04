# Tópico SNS para alertas de anomalia
# Always Free: primeiras 1.000 notificações por email/mês grátis (para sempre)

resource "aws_sns_topic" "vero_alertas" {
  name = "${var.project_name}-alertas"

  tags = {
    Project = var.project_name
  }
}

# Exemplo de subscription por email (descomentar e configurar quando quiser receber alertas)
# resource "aws_sns_topic_subscription" "email_alerta" {
#   topic_arn = aws_sns_topic.vero_alertas.arn
#   protocol  = "email"
#   endpoint  = "seu-email@exemplo.com"
# }
