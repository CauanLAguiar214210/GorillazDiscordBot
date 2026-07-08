output "ecr_repository_url" {
  description = "URL do repositório ECR"
  value       = module.ecr.repository_url
}

output "ecs_cluster_name" {
  description = "Nome do cluster ECS"
  value       = module.ecs.cluster_name
}

output "ecs_service_name" {
  description = "Nome do serviço ECS"
  value       = module.ecs.service_name
}

output "ecs_task_definition_arn" {
  description = "ARN da task definition"
  value       = module.ecs.task_definition_arn
}

output "secrets_discord_token_arn" {
  description = "ARN do secret do Discord Token (precisa atualizar o valor)"
  value       = module.secrets.discord_token_arn
}

output "secrets_mongodb_connection_arn" {
  description = "ARN do secret do MongoDB (precisa atualizar o valor)"
  value       = module.secrets.mongodb_connection_arn
}

output "secrets_owm_api_key_arn" {
  description = "ARN do secret da OWM API Key (precisa atualizar o valor)"
  value       = module.secrets.owm_api_key_arn
}

output "secrets_update_commands" {
  description = "Comandos para atualizar os secrets no AWS CLI"
  value = <<-EOT
    Execute os comandos abaixo para definir os valores reais dos secrets:
    aws secretsmanager put-secret-value --secret-id ${module.secrets.discord_token_arn} --secret-string "seu-token-aqui"
    aws secretsmanager put-secret-value --secret-id ${module.secrets.owm_api_key_arn} --secret-string "sua-api-key-aqui"
    aws secretsmanager put-secret-value --secret-id ${module.secrets.mongodb_connection_arn} --secret-string "mongodb+srv://..."
  EOT
}
