variable "aws_region" {
  description = "Região AWS onde os recursos serão criados"
  type        = string
  default     = "us-east-1"
}

variable "project_name" {
  description = "Nome do projeto usado para nomear recursos"
  type        = string
  default     = "gorillaz-discord-bot"
}

variable "vpc_cidr" {
  description = "CIDR block da VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "subnet_cidr" {
  description = "CIDR block da subnet pública"
  type        = string
  default     = "10.0.1.0/24"
}

variable "mongodb_database" {
  description = "Nome do banco de dados MongoDB"
  type        = string
  default     = "gorillazbot"
}

variable "command_prefix" {
  description = "Prefixo dos comandos do bot"
  type        = string
  default     = "macaco "
}

variable "task_cpu" {
  description = "CPU da tarefa ECS (Fargate)"
  type        = number
  default     = 256
}

variable "task_memory" {
  description = "Memória da tarefa ECS (Fargate)"
  type        = number
  default     = 512
}

variable "discord_token" {
  description = "Token do bot do Discord (placeholder, atualizar no Secrets Manager)"
  type        = string
  sensitive   = true
  default     = "troque-pelo-seu-token"
}

variable "owm_api_key" {
  description = "Chave da API OpenWeatherMap (placeholder)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "mongodb_connection_string" {
  description = "Connection string do MongoDB Atlas"
  type        = string
  sensitive   = true
  default     = "troque-pela-sua-connection-string"
}
