variable "project"              { type = string }
variable "vpc_id"               { type = string }
variable "public_subnet_ids"    { type = list(string) }
variable "security_group_id"    { type = string }
variable "ecr_repository_url"   { type = string }
variable "discord_token_arn"    { type = string }
variable "owm_api_key_arn"      { type = string }
variable "mongodb_conn_arn"     { type = string }
variable "mongodb_database"     { type = string }
variable "command_prefix"       { type = string }
variable "task_cpu"             { type = number }
variable "task_memory"          { type = number }


resource "aws_ecs_cluster" "main" {
  name = var.project
  tags = { Name = var.project }
}

resource "aws_cloudwatch_log_group" "main" {
  name              = "/ecs/${var.project}"
  retention_in_days = 30
  tags              = { Name = "${var.project}-logs" }
}

resource "aws_ecs_task_definition" "main" {
  family                   = var.project
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.task_cpu
  memory                   = var.task_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name  = "bot"
      image = "${var.ecr_repository_url}:latest"
      essential = true

      environment = [
        { name = "MONGODB_DATABASE_NAME", value = var.mongodb_database },
        { name = "COMMAND_PREFIX",        value = var.command_prefix },
        { name = "TZ",                    value = "America/Sao_Paulo" },
        { name = "DOTNET_ENVIRONMENT",    value = "Production" }
      ]

      secrets = [
        { name = "DISCORD_TOKEN",              valueFrom = var.discord_token_arn },
        { name = "OWM_API_KEY",                valueFrom = var.owm_api_key_arn },
        { name = "MONGODB_CONNECTION_STRING",  valueFrom = var.mongodb_conn_arn }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.main.name
          "awslogs-region"        = data.aws_region.current.name
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])

  tags = { Name = "${var.project}-task-def" }
}

data "aws_region" "current" {}

resource "aws_ecs_service" "main" {
  name                   = var.project
  cluster                = aws_ecs_cluster.main.id
  task_definition        = aws_ecs_task_definition.main.arn
  desired_count          = 1
  launch_type            = "FARGATE"
  enable_execute_command = false

  network_configuration {
    subnets         = var.public_subnet_ids
    security_groups = [var.security_group_id]
    assign_public_ip = true
  }

  tags = { Name = "${var.project}-service" }
}

output "cluster_name"       { value = aws_ecs_cluster.main.name }
output "service_name"       { value = aws_ecs_service.main.name }
output "task_definition_arn" { value = aws_ecs_task_definition.main.arn }
output "log_group_name"     { value = aws_cloudwatch_log_group.main.name }
