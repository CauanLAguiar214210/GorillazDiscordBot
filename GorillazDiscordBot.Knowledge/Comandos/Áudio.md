---
tags:
  - comandos
  - audio
atualizado: 2026-09-28
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
| `macaco tocar <origem>` | `/audio tocar <origem>` | Toca o áudio no canal de quem chamou. |
| `macaco teste` | `/audio teste` | Toca o som de teste (`oleodemacaco.mp3`) — valida o pipeline. |
| `macaco parar` | `/audio parar` | Para a reprodução e desconecta. |
| `macaco sons` | `/audio sons` | Lista sons de `Api/Resources/Sounds/`. |
| `macaco agendar <origem> <HH:MM> [dia] [canal]` | `/audio agendar …` | Agenda som em horário fixo (admin). |
| `macaco agendamentos` | `/audio agendamentos` | Lista agendamentos (admin). |
| `macaco desagendar <id>` | `/audio desagendar <id>` | Remove agendamento (admin). |
| `macaco som-entrar <origem\|off>` | `/audio som-entrada <origem\|off>` | Som ao **entrar** no canal de voz (admin). |
| `macaco som-sair <origem\|off>` | `/audio som-saida <origem\|off>` | Som ao **sair** do canal de voz (admin). |
| `macaco som-status` | `/audio som-status` | Mostra os sons de entrada/saída (admin). |

## Origens aceitas

| Origem | Exemplo | Quem pode | Como toca |
| --- | --- | --- | --- |
| Link do YouTube | `https://www.youtube.com/watch?v=…`, `https://youtu.be/…` | qualquer membro | source `youtube` (plugin) |
| Busca | `ytsearch:termo`, `ytmsearch:termo` ou `!termo` | qualquer membro | source `youtube` (plugin) |
| Som instantâneo | `https://www.myinstants.com/instant/nome-do-som-1234` | qualquer membro | convertido para MP3 e source `http` |
| URL direta de áudio | `https://cdn.exemplo.com/som.mp3` | **ManageGuild** | source `http` |
| Som local | `local:alarme.mp3` | qualquer membro | source `local` |

> **Segurança**: só **URLs `http/https` de domínios arbitrários** ficam restritas a **ManageGuild** (o container pode buscar qualquer endereço). YouTube, busca e Myinstants são liberados para membros comuns. O `local:` nunca aceita `..`/caminho absoluto.

## Setup

- **Lavalink ≥ 4.2.0** — o Discord passou a exigir **DAVE/E2EE** em 02/03/2026; servidor antigo cai com close code **4017** (`E2EE/DAVE protocol required`, logger `moe.kyokobot.koe`), silenciando o áudio mesmo com o player "ok". O client usa **Lavalink4NET 4.2.2** (≥ 4.1.0 adiciona `channelId` ao voice state, exigido pelo DAVE).
- Sons locais: `Api/Resources/Sounds/` → `sounds/` na imagem Lavalink (`lavalink/Dockerfile`).
- YouTube: o Lavalink 4 não traz mais a fonte embutida. O `lavalink/Dockerfile` baixa `dev.lavalink.youtube:youtube-plugin:1.18.2` para `/opt/Lavalink/plugins/` no build (bump em `ARG YOUTUBE_SOURCE_VERSION`), e o `lavalink/application.yml` liga `plugins.youtube` (`enabled`, `allowSearch`, `allowDirectVideoIds`, `allowDirectPlaylistIds`). A fonte embutida `lavalink.server.sources.youtube` fica **`false`** de propósito — o plugin exige isso. Se faltar o jar, o bot responde com a dica de verificar o plugin (não um erro genérico).
- Myinstants: `www.myinstants.com` bloqueia requisição sem browser (403), então o bot não copia o áudio — ele extrai o *slug* da página e monta o MP3 direto no espelho (`AUDIO_INSTANT_MIRROR_BASE_URL`, default `https://myinstants.site`), que o Lavalink baixa pela source `http`.

## Detalhes

- `AudioPlayerService` usa `PlayerRetrieveOptions(Join, Ignore, sessão vazia)`; `DisconnectOnStop=false` e desconexão explícita em `TrackEnded`/`TrackException`/`TrackStuck` (toca-e-sai).
- `AudioTrackResolver` é puro/síncrono e classifica a origem em `AudioOriginKind` (`Local`, `YouTube`, `YouTubeSearch`, `InstantButton`, `DirectUrl`); só `DirectUrl` vira `RequiresElevatedPermission`. `AudioPlayerService.ResolveAsync` resolve o `InstantButton` via `IInstantSoundResolver` (HTTP + cache de 6h) e troca o identifier pelo MP3.
- O bot guarda o identifier com prefixo no `local:` (`local:sounds/x.mp3`) e converte no envio para o Lavalink (`ToServerIdentifier` → `sounds/x.mp3`), porque o `LocalAudioSourceManager` do Lavalink 4 resolve o caminho literal relativo ao workdir (`/opt/Lavalink`, com sons em `sounds/`).
- YouTube/Spotify: links de página do YouTube passam direto para o Lavalink (o plugin entende `watch`, `youtu.be`, `shorts`, `list`). **Spotify continua sem suporte** — exigiria o plugin `lavasrc` e o resolver `Lavalink4NET.Spotify`.
- Agendamentos persistidos em `Guild.ScheduledSounds` (Mongo) — sobrevivem a restart — e avaliados por `ScheduledSoundService` a cada 30s; só tocam com gente no canal. Guardam a **origem** (`AudioSource`), não os bytes do arquivo.
- Horários são convertidos para UTC no cadastro e comparados em UTC no disparo; o **dia da semana** é comparado no fuso de Brasília (`ScheduleEvaluator.ShouldFire` converte para `America/Sao_Paulo` antes) — evita o disparo "1 dia antes" em horários noturnos (ex.: `segunda 22:00`). O código usa `DateTime.UtcNow` (nunca o relógio do servidor); o offset de SP é logado no boot: `Fuso de referência America/Sao_Paulo resolvido - offset UTC atual -03:00:00`.
- Sons de entrada/saída (`Guild.JoinVoiceSound`/`LeaveVoiceSound`, Mongo) tocados via `JoinLeaveSoundService` (evento `UserVoiceStateUpdated`), com **cooldown de 15s por usuário** e ignorando o próprio bot.