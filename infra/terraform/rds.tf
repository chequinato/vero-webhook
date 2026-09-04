# RDS PostgreSQL — SOMENTE para terraform plan, NÃO fazer apply
# O free tier do RDS depende da idade da conta AWS (12 meses).
# Roda local com Postgres; esse arquivo é para estudo/documentação.

resource "aws_db_instance" "vero_postgres" {
  identifier     = "${var.project_name}-postgres"
  engine         = "postgres"
  engine_version = "16.4"
  instance_class = "db.t3.micro"

  allocated_storage     = 20
  max_allocated_storage = 20
  storage_type          = "gp2"

  db_name  = "vero"
  username = var.db_username
  password = var.db_password

  publicly_accessible    = false
  skip_final_snapshot    = true
  deletion_protection    = false
  backup_retention_period = 0

  tags = {
    Project = var.project_name
    Warning = "NAO-APLICAR-SEM-FREE-TIER"
  }
}
