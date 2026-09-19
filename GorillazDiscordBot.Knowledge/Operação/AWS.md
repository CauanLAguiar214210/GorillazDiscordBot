---
tags:
  - operacao
  - aws
  - ecs
atualizado: 2026-09-19
---

# AWS/ECS

[[Operação|← Operação]] · [[Operação/Execução local|Execução local]] · [[Operação/Logs e observabilidade|Logs]]

Visão de entrega na AWS. Detalhes e problemas na [[Investigação/Problema ECS|Investigação: Problema ECS]].

## Peças

- `Dockerfile` — imagem Linux do bot.
- `docker-compose.yml` — execução local composta.
- `.github/workflows/ci.yml` — CI.
- `.github/workflows/aws.yml` — entrega (publica imagem, registra task, atualiza serviço).
- `task-definition.template.json` — task com containers `bot` e `luckymonkey` (sidecar).
- `Infra/AWS/` (Terraform: `ecr/`, `ecs/`, `secrets/`) — infraestrutura.

## Pontos de atenção conhecidos

- O Terraform atualmente só define o container `bot` e apenas os segredos Discord/OWM/MongoDB; o sidecar exige imagem e segredos/ARNs adicionais.
- Sem health check configurado, uma task pode parecer saudável mesmo com o bot desconectado.
- A propagação de slash commands globais é lenta; use `DISCORD_DEV_GUILD_ID` para testar.

Candidato a consolidação: [[Ideias/Reconciliar task definition com IaC|Reconciliar task definition com IaC]].