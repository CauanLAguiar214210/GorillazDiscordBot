# IAM role for ECS task execution (pull image, push logs)
resource "aws_iam_role" "execution" {
  name = "${var.project}-ecs-execution-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# IAM role for the task itself (read secrets from Secrets Manager)
resource "aws_iam_role" "task" {
  name = "${var.project}-ecs-task-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_policy" "secrets_access" {
  name        = "${var.project}-secrets-access"
  description = "Allow ECS task to read project secrets"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = [
          var.discord_token_arn,
          var.owm_api_key_arn,
          var.mongodb_conn_arn
        ]
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "secrets_access_task" {
  role       = aws_iam_role.task.name
  policy_arn = aws_iam_policy.secrets_access.arn
}

resource "aws_iam_role_policy_attachment" "secrets_access_execution" {
  role       = aws_iam_role.execution.name
  policy_arn = aws_iam_policy.secrets_access.arn
}

output "execution_role_arn" { value = aws_iam_role.execution.arn }
output "task_role_arn" { value = aws_iam_role.task.arn }
