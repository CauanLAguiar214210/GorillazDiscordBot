---
tags:
  - onboarding
  - guia
atualizado: 2026-09-19
---

# Onboarding

[[Home|Home]] · [[Desenvolvimento|Desenvolvimento]] · [[Segurança|Segurança]]

Roteiro para quem chega no projeto.

## Ordem de leitura

1. [[Home|Home]] — o que é o bot.
2. [[Arquitetura/Visão geral|Visão geral]] — visão macro, projetos e camadas.
3. [[Arquitetura/Backend|Backend]] — bootstrap, ciclo de vida e fluxo de comandos.
4. [[Arquitetura/Banco de dados|Banco de dados]] — persistência e estado.
5. [[Arquitetura/Infraestrutura|Infraestrutura]] — ambiente, ECS e operação.
6. [[Desenvolvimento/Testes e convenções|Testes e convenções]] — como rodar e o que respeitar.

Aprofunde por demanda em [[Comandos|Comandos]], [[Integrações|Integrações]] e [[Operação|Operação]].

## Setup (10–15 min)

1. `cp GorillazDiscordBot.Api/.env.example GorillazDiscordBot.Api/.env` e preencher `DISCORD_TOKEN`.
2. Definir `DISCORD_DEV_GUILD_ID` para registrar slash commands localmente.
3. `docker-compose up` (MongoDB + bot) — o LuckyMonkey **não** sobe localmente.
4. `dotnet restore GorillazDiscordBot.sln` e `dotnet test` (suíte xUnit; ~88 testes).
5. Conferir a convenção de execução em [[Operação/Execução local|Execução local]] e os comandos em [[Comandos|Comandos]].

## Comunicação

- `dev` recebe pushes (CI roda); PRs miram `master` (deploy) — detalhes em [[Git e entrega|Git e entrega]].
- Antes de expor segredos ou logs, leia [[Segurança|Segurança]].