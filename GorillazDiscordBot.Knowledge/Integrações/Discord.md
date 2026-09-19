---
tags:
  - integracao
  - discord
atualizado: 2026-09-19
---

# Discord

[[Integrações|← Integrações]]

Integração com o gateway do Discord via **Discord.Net**.

## Setup

- **Token:** `DISCORD_TOKEN` (obrigatório; vazio impede a conexão).
- **Intents:** `Guilds`, `GuildMessages`, `MessageContent`, `DirectMessages`, `GuildMembers`, `GuildVoiceStates` — intents privilegiadas (`MessageContent`, `GuildMembers`) exigem habilitação no Portal.
- **Escopos do convite:** `bot` e `applications.commands`, com permissões nos canais de uso.
- **Dev:** `DISCORD_DEV_GUILD_ID` registra slash commands localmente; sem ele, o registro é global e a propagação é lenta.

## Intervalo com investigação

Problemas de conexão/mensagens/slash commands: [[Investigação/Problema Discord|Problema Discord]]. Candidato a ideia: [[Ideias/Validar intents e escopos no Portal Discord|Validar intents e escopos]].