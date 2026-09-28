---
tags:
  - operacao
  - local
atualizado: 2026-09-27
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
| `LAVALINK_REST_URI` / `LAVALINK_WS_URI` / `LAVALINK_PASSWORD` / `LAVALINK_LOCAL_AUDIO_PATH` | Servidor de música Lavalink. |

## Passos

1. Copiar `GorillazDiscordBot.Api/.env.example` para `.env` e preencher (arquivo ignorado pelo Git).
2. Subir dependências: `docker-compose up` (Mongo + Lavalink + bot; o LuckyMonkey **não** é iniciado pelo compose).
3. Usar `DISCORD_DEV_GUILD_ID` para registro de slash commands instantâneo.
4. Rodar seeds de `scripts/mongodb/` apenas no banco correto.

## Caveats

- A url `LUCKY_MONKEY_URL=http://localhost:8080` só funciona com o sidecar — a paridade local é reduzida sem ele.
- Sessões em memória (quiz, trabalho, manobrista, apostas) não sobrevivem a reinício. Ver [[Arquitetura/Banco de dados|Banco de dados]].
- **Lavalink ≥ 4.2.0** obrigatório: o Discord exige DAVE/E2EE desde 02/03/2026; versões antigas caem com close **4017** e o áudio fica mudo. O `lavalink/Dockerfile` usa `ARG LAVALINK_VERSION=4.2.2`.
- O container do bot (`Dockerfile` Alpine) instala `tzdata`; sem ele o `America/Sao_Paulo` falha e o agendamento de áudio cai para UTC (horários errados em até ~3h). Check no boot: `Fuso de referência America/Sao_Paulo resolvido - offset UTC atual -03:00:00`.