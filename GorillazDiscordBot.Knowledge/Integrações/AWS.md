---
tags:
  - integracao
  - aws
atualizado: 2026-09-19
---

# AWS

[[Integrações|← Integrações]] · [[Operação/AWS|AWS/ECS]]

Serviços AWS envolvidos na entrega e operação.

| Serviço | Uso |
| --- | --- |
| ECR | Repositório da imagem `gorillaz-discord-bot`. |
| ECS | Execução da task (bot + sidecar). |
| CloudWatch | Logs (`AWS_LOG_GROUP`/`AWS_REGION`). |
| Secrets Manager | Segredos injetados na task (`DISCORD_TOKEN`, OWM, MongoDB e, na intenção, LuckyMonkey). |

Divergências entre task definition, Terraform e workflow: [[Investigação/Problema ECS|Problema ECS]].