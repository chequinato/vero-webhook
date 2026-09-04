variable "aws_region" {
  description = "Região da AWS"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Nome do projeto (usado como prefixo nos recursos)"
  type        = string
  default     = "vero"
}

variable "db_username" {
  description = "Usuário do banco de dados RDS"
  type        = string
  default     = "vero_admin"
  sensitive   = true
}

variable "db_password" {
  description = "Senha do banco de dados RDS"
  type        = string
  sensitive   = true
}

variable "db_connection_string" {
  description = "Connection string do PostgreSQL para a Lambda"
  type        = string
  sensitive   = true
  default     = ""
}
