---
tags:
  - comandos
  - audio
atualizado: 2026-09-27
---

# Áudio

[[Comandos|← Comandos]]

Toca sons no canal de voz via **Lavalink** (sidecar). Modelo "toque e saia": sai do canal quando o áudio termina.

## Comandos

| Módulo | Local |
| --- | --- |
| `AudioPrefixModule` | `Commands/Audio/AudioPrefixModule.cs` |
| `AudioSlashModule` | `Commands/Audio/AudioSlashModule.cs` |
| `JoinLeaveSoundService` | `Services/JoinLeaveSoundService.cs` |

Disponível por **prefixo** e por **slash** (`/audio <comando>`):

| Prefixo | Slash | Descrição |
| --- | --- | --- |
| `macaco tocar <origem>` | `/audio tocar <origem>` | Toca URL ou som local no canal de quem chamou. |
| `macaco teste` | `/audio teste` | Toca o som de teste (`oleodemacaco.mp3`) — valida o pipeline. |
| `macaco parar` | `/audio parar` | Para a reprodução e desconecta. |
| `macaco sons` | `/audio sons` | Lista sons de `Api/Resources/Sounds/`. |
| `macaco agendar <origem> <HH:MM> [dia] [canal]` | `/audio agendar …` | Agenda som em horário fixo (admin). |
| `macaco agendamentos` | `/audio agendamentos` | Lista agendamentos (admin). |
| `macaco desagendar <id>` | `/audio desagendar <id>` | Remove agendamento (admin). |
| `macaco som-entrar <origem\|off>` | `/audio som-entrada <origem\|off>` | Som ao **entrar** no canal de voz (admin). |
| `macaco som-sair <origem\|off>` | `/audio som-saida <origem\|off>` | Som ao **sair** do canal de voz (admin). |
| `macaco som-status` | `/audio som-status` | Mostra os sons de entrada/saída (admin). |

> **Segurança**: URLs `http/https` em `tocar`/`agendar`/sons de entrada-saída exigem **ManageGuild** (o container pode buscar qualquer endereço). Membros comuns usam apenas `local:`. O `local:` nunca aceita `..`/caminho absoluto.

## Setup

- **Lavalink ≥ 4.2.0** — o Discord passou a exigir **DAVE/E2EE** em 02/03/2026; servidor antigo cai com close code **4017** (`E2EE/DAVE protocol required`, logger `moe.kyokobot.koe`), silenciando o áudio mesmo com o player "ok". O client usa **Lavalink4NET 4.2.2** (≥ 4.1.0 adiciona `channelId` ao voice state, exigido pelo DAVE).
- Sons locais: `Api/Resources/Sounds/` → `sounds/` na imagem Lavalink (`lavalink/Dockerfile`).

## Detalhes

- `AudioPlayerService` usa `PlayerRetrieveOptions(Join, Ignore, sessão vazia)`; `DisconnectOnStop=false` e desconexão explícita em `TrackEnded`/`TrackException`/`TrackStuck` (toca-e-sai).
- Origens: URLs `https/http` ou `local:<arquivo>` (validadas em `AudioTrackResolver`, sem `..`/absolutos). O bot guarda o identifier com prefixo (`local:sounds/x.mp3`) e converte no envio para o Lavalink (`ToServerIdentifier` → `sounds/x.mp3`), porque o `LocalAudioSourceManager` do Lavalink 4 resolve o caminho literal relativo ao workdir (`/opt/Lavalink`, com sons em `sounds/`).
- Agendamentos persistidos em `Guild.ScheduledSounds` (Mongo) — sobrevivem a restart — e avaliados por `ScheduledSoundService` a cada 30s; só tocam com gente no canal. Guardam a **origem** (`AudioSource`), não os bytes do arquivo.
- Horários são convertidos para UTC no cadastro e comparados em UTC no disparo; o **dia da semana** é comparado no fuso de Brasília (`ScheduleEvaluator.ShouldFire` converte para `America/Sao_Paulo` antes) — evita o disparo "1 dia antes" em horários noturnos (ex.: `segunda 22:00`). O código usa `DateTime.UtcNow` (nunca o relógio do servidor); o offset de SP é logado no boot: `Fuso de referência America/Sao_Paulo resolvido - offset UTC atual -03:00:00`.
- Sons de entrada/saída (`Guild.JoinVoiceSound`/`LeaveVoiceSound`, Mongo) tocados via `JoinLeaveSoundService` (evento `UserVoiceStateUpdated`), com **cooldown de 15s por usuário** e ignorando o próprio bot.
- Links de página **YouTube/Spotify** ainda não são resolvidos (só áudio direto por URL). Exigiria o plugin `youtube-source` (lavalink) e o resolver `Lavalink4NET.Spotify` — custo R$ 0 em ambos.