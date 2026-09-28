---
tags:
  - integracao
  - discord
atualizado: 2026-09-27
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

## Áudio (Lavalink)

Reprodução de voz usa o **Lavalink4NET 4.2.2** via sidecar `gorillaz-lavalink` (porta `2333`) contra `DiscordSocketClient` — registrado em `Program.cs` com `AddLavalink()`/`ConfigureLavalink()` após o client. Sons locais ficam em `Api/Resources/Sounds/` (copiados para `sounds/` na imagem Lavalink) e são acessados como `local:<arquivo>`. Em ECS o bot usa `http://localhost:2333` (mesmo pattern do LuckyMonkey) e o WebSocket em `ws://localhost:2333/v4/websocket` — o path `/v4/websocket` é obrigatório no Lavalink ≥ 4. Sem ele a conexão cai no shim legado do v3, que responde 200 mas nunca fica ready e o comando retorna "Servidor de música (Lavalink) indisponível". Ver [[Comandos/Áudio|Áudio]].

> **DAVE/E2EE**: desde 02/03/2026 o Discord exige o protocolo DAVE para calls não-stage. Lavalink **≥ 4.2.0** e client **Lavalink4NET ≥ 4.1.0** são obrigatórios; stacks antigas caem com close code **4017** (`E2EE/DAVE protocol required`, logger `moe.kyokobot.koe`) — o player parece ok, mas o áudio chega mudo ao canal.