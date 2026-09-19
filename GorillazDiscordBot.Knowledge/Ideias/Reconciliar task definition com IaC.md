---
tags:
  - ideias
  - ecs
  - terraform
status: ideia
atualizado: 2026-09-19
---

# Reconciliar task definition com IaC

[[Ideias|← Ideias]]

Manter a `task-definition.template.json` como fonte única da task, provisionando/versionando no Terraform todos os recursos que o sidecar exige (imagem LuckyMonkey no ECR, segredos e permissões, grupo de log `/ecs/gorillaz-discord-bot/luckymonkey`), e alinhando os containers declarados no IaC com os do workflow.

**Motivação:** [[Investigação/Problema ECS|Problema ECS]] (deriva entre Terraform e workflow).