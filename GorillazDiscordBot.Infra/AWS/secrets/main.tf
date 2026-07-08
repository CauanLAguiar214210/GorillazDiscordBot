variable "project" { type = string }

resource "aws_secretsmanager_secret" "discord_token" {
  name        = "${var.project}/discord-token"
  description = "Discord bot token"

  tags = { Name = "${var.project}-discord-token" }
}

resource "aws_secretsmanager_secret" "owm_api_key" {
  name        = "${var.project}/owm-api-key"
  description = "OpenWeatherMap API key"

  tags = { Name = "${var.project}-owm-api-key" }
}

resource "aws_secretsmanager_secret" "mongodb_connection" {
  name        = "${var.project}/mongodb-connection"
  description = "MongoDB Atlas connection string"

  tags = { Name = "${var.project}-mongodb-connection" }
}

# Placeholder values — usuário deve atualizar via CLI/console
resource "aws_secretsmanager_secret_version" "discord_token" {
  secret_id     = aws_secretsmanager_secret.discord_token.id
  secret_string = var.discord_token_placeholder
}

resource "aws_secretsmanager_secret_version" "owm_api_key" {
  secret_id     = aws_secretsmanager_secret.owm_api_key.id
  secret_string = var.owm_api_key_placeholder != "" ? var.owm_api_key_placeholder : "not-configured"
}

resource "aws_secretsmanager_secret_version" "mongodb_connection" {
  secret_id     = aws_secretsmanager_secret.mongodb_connection.id
  secret_string = var.mongodb_connection_placeholder
}

variable "discord_token_placeholder" {
  type    = string
  default = "troque-pelo-seu-token"
}

variable "owm_api_key_placeholder" {
  type    = string
  default = ""
}

variable "mongodb_connection_placeholder" {
  type    = string
  default = "troque-pela-sua-connection-string"
}

output "discord_token_arn" { value = aws_secretsmanager_secret.discord_token.arn }
output "owm_api_key_arn" { value = aws_secretsmanager_secret.owm_api_key.arn }
output "mongodb_connection_arn" { value = aws_secretsmanager_secret.mongodb_connection.arn }
