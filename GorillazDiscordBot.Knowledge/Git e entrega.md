---
tags:
  - git
  - entrega
atualizado: 2026-09-19
---

# Git e entrega

[[Home|Home]] · [[Operação/AWS|AWS/ECS]] · [[Operação/Execução local|Execução local]]

Fluxo de integração e entrega atual do repositório.

## Fluxo de branches

- **`dev`** — integração: `push` executa o workflow `ci.yml` (build + teste).
- **`master`** — produção: *pull requests* executam CI; `push` em `master` dispara o deploy no `aws.yml`.
- PRs devem ser abertos de feature branches para `master` (CI valida antes do merge).

## CI (`ci.yml`)

- Ubuntu `latest`, instala .NET 9 SDK.
- Autentica a fonte NuGet privada `github-luckymonkey` com `GITHUB_TOKEN` (restore das dependências do cassino).
- `restore` → `build -c Release` → `test -c Release --no-build`.

## Deploy (`aws.yml`)

- Roda após CI, via `workflow_call`, com `concurrency: deploy-production` (sem cancelamento em andamento).
- Builda/publica a imagem ECR `gorillaz-discord-bot` com tags `:${sha}` e `:latest` (secrets: `ACOUGUE_MINERIO_TOKEN`).
- Renderiza `task-definition.template.json` via `envsubst` (vars: `AWS_ACCOUNT_ID`, `*_ARN` dos segredos).
- Registra e atualiza o serviço ECS `gorillaz-discord-bot` (cluster homônimo) com `wait-for-service-stability`.

> Caminho do Terraform: `GorillazDiscordBot.Infra/AWS/{ecr,ecs,networking,secrets}/`. Divergências atuais: [[Investigação/Problema ECS|Problema ECS]]; candidatos a melhoria em [[Ideias|Ideias]].

## Convenções propostas

- Commits atômicos e descritivos; testes passando antes do PR (`dotnet test GorillazDiscordBot.sln`).
- Nada de segredos no diff — revisar com [[Segurança|Segurança]].
- Registrar mudanças de comportamento no [[Changelog|Changelog]].