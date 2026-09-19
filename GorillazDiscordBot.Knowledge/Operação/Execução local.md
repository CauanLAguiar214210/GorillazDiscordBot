---
tags:
  - operacao
  - local
atualizado: 2026-09-19
---

# Execução local

[[Operação|← Operação]] · [[Operação/AWS|AWS/ECS]] · [[Operação/Troubleshooting|Troubleshooting]]

## Variáveis

| Variável | Finalidade |
| --- | --- |
| `DISCORD_TOKEN` | Token do bot. |
| `COMMAND_PREFIX` | Prefixo padrão (padrão `macaco `). |
| `MONGODB_CONNECTION_STRING` / `MONGODB_DATABASE_NAME` | MongoDB (padrão `gorillazbot`). |
| `DISCORD_DEV_GUILD_ID` | Guild de dev para registrar slash commands locais. |
| `LUCKY_MONKEY_URL` / `LUCKY_MONKEY_API_KEY` / `CASINO_JWT_SIGNING_KEY` | Cassino. |
| `AWS_LOG_GROUP` / `AWS_REGION` | Logs no CloudWatch (opcional). |

## Passos

1. Copiar `GorillazDiscordBot.Api/.env.example` para `.env` e preencher (arquivo ignorado pelo Git).
2. Subir dependências: `docker-compose up` (Mongo + bot; o LuckyMonkey **não** é iniciado pelo compose).
3. Usar `DISCORD_DEV_GUILD_ID` para registro de slash commands instantâneo.
4. Rodar seeds de `scripts/mongodb/` apenas no banco correto.

## Caveats

- A url `LUCKY_MONKEY_URL=http://localhost:8080` só funciona com o sidecar — a paridade local é reduzida sem ele.
- Sessões em memória (quiz, trabalho, manobrista, apostas) não sobrevivem a reinício. Ver [[Arquitetura/Banco de dados|Banco de dados]].