﻿---
tags:
  - comandos
  - audio
tipo: nota
atualizado: 2026-10-08
---

# Áudio

[[Comandos|← Comandos]]

Toca sons no canal de voz via **Lavalink** (sidecar). O modo padrão é "toque e saia"; use `join` para manter o bot conectado até `leave` ou até o canal ficar sem usuários humanos.

## Comandos

| Módulo | Local |
| --- | --- |
| `AudioPrefixModule` | `Commands/Audio/AudioPrefixModule.cs` |
| `AudioSlashModule` | `Commands/Audio/AudioSlashModule.cs` |
| `JoinLeaveSoundService` | `Services/JoinLeaveSoundService.cs` |
| `AudioUploadService` | `Services/AudioUploadService.cs` (limpeza dos uploads legados) |
| `AudioUploadCleanupService` | `Services/AudioUploadCleanupService.cs` (limpeza periódica) |
| `FavoriteSoundMatcher` | `Services/FavoriteSoundMatcher.cs` (apelido/origem, limites e dedupe) |

Disponível por **prefixo** e por **slash** (`/audio <comando>`):

| Prefixo | Slash | Descrição |
| --- | --- | --- |
| `macaco tocar <origem>` | `/audio tocar <origem>` | Toca o áudio no canal de quem chamou. |

| `macaco join [canal]` | `/audio join [canal]` | Entra e permanece no canal. Qualquer membro pode usar. |
| `macaco leave` | `/audio leave` | Para o áudio e faz o bot sair do canal. Qualquer membro pode usar. |
| `macaco teste` | `/audio teste` | Toca o som de teste (`oleodemacaco.mp3`) — valida o pipeline. |
| `macaco parar` | `/audio parar` | Para a reprodução; em modo `join`, mantém o bot conectado. |
| `macaco sons` | `/audio listar` | Lista sons locais e favoritos com ações interativas. |
| `macaco agendar <origem> <HH:MM> [dia] [canal]` | `/audio agendar …` | Agenda som em horário fixo (admin). |
| `macaco agendamentos` | `/audio agendamentos` | Lista agendamentos (admin). |
| `macaco desagendar <id>` | `/audio desagendar <id>` | Remove agendamento (admin). |
| `macaco som-entrar <origem\|off>` | `/audio som-entrada <origem\|off>` | Som ao **entrar** no canal de voz (admin). |
| `macaco som-sair <origem\|off>` | `/audio som-saida <origem\|off>` | Som ao **sair** do canal de voz (admin). |
| `macaco som-status` | `/audio som-status` | Mostra os sons de entrada/saída (admin). |

## Favoritos da guilda

`/audio listar` guarda a navegação do catálogo em uma mensagem interativa. O filtro
`⭐ Favoritos` mostra os itens de `Guild.FavoriteSounds` (Mongo), até **100** por guilda.
Cada favorito tem apelido opcional (até **32** caracteres), origem e contador de reproduções.

- As ações ficam nos botões da lista (detail **Tocar**/**Voltar**/**Favoritar**/
  **Desfavoritar**/**Remover**). Não há comandos de favorito por prefixo; não-local novo entra
  via `/audio add <origem> <nome>`, e local favoritá-se pelo próprio botão na lista.
- Os botões do catálogo identificam o item pela **chave da origem** (hash de 16 hex —
  `AudioSlashModule.CatalogKey`), não pelo índice da página: outra pessoa favoritando entre
  render e clique não desvia a ação para o áudio errado.
- Quem cria e remove precisa de **ManageGuild** (ou `Administrator`). Ao **tocar**, a permissão de URL direta é **reavaliada**: um favorito `DirectUrl`
  criado por admin e depois tocado por um membro sem permissão é recusado na hora.
- `/audio add` salva a **origem digitada** (ex.: a URL do Myinstants), não a URL do espelho
  resolvida — o espelho pode mudar sem quebrar o favorito.
- O fluxo de anexo (`favoritar-anexo`) não existe mais: o bot não baixa arquivos do CDN do
  Discord. Origens `local:uploads/…` já existentes continuam tocando.
- `AudioUploadCleanupService` roda no boot e a cada 6h apagando os arquivos `<guid>.<ext>`
  gerados pelo bot mais velhos que `AUDIO_UPLOAD_RETENTION_DAYS` (30). O critério é a
  **idade do arquivo**: um áudio de upload mais antigo que a retenção some do disco e para
  de tocar.


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
- **Uploads legados**: o fluxo de upload de anexos foi removido, mas os arquivos já gravados
  seguem tocáveis — `docker-compose.yml` monta `audio_uploads` em `/audio/uploads` (bot,
  `AUDIO_UPLOAD_PATH`) e em `/opt/Lavalink/sounds/uploads` (Lavalink), então a origem
  `local:uploads/…` é servida como `sounds/uploads/…`. Sem o volume montado nos dois lados, o
  Lavalink responde vazio ao validar.
- YouTube: o Lavalink 4 não traz mais a fonte embutida. O `lavalink/Dockerfile` baixa `dev.lavalink.youtube:youtube-plugin:1.18.2` para `/opt/Lavalink/plugins/` no build (bump em `ARG YOUTUBE_SOURCE_VERSION`), e o `lavalink/application.yml` liga `plugins.youtube` (`enabled`, `allowSearch`, `allowDirectVideoIds`, `allowDirectPlaylistIds`). A fonte embutida `lavalink.server.sources.youtube` fica **`false`** de propósito — o plugin exige isso. Se faltar o jar, o bot responde com a dica de verificar o plugin (não um erro genérico).
- Myinstants: `www.myinstants.com` bloqueia requisição sem browser (403), então o bot não copia o áudio — ele extrai o *slug* da página e monta o MP3 direto no espelho (`AUDIO_INSTANT_MIRROR_BASE_URL`, default `https://myinstants.site`), que o Lavalink baixa pela source `http`.

## Detalhes

- `AudioPlayerService` usa `PlayerRetrieveOptions(Join, Ignore, sessão vazia)`; `DisconnectOnStop=false` e desconexão explícita em `TrackEnded`/`TrackException`/`TrackStuck` (toca-e-sai).
- **Início de reprodução é confirmado**: depois do `PlayAsync` o serviço espera o evento `TrackStarted` do Lavalink por `AUDIO_PLAYBACK_START_TIMEOUT_SECONDS` (default 15s). Sem essa confirmação, o bot ficava em silêncio na call sem explicar nada; agora responde `O áudio não começou a tocar em Ns`.
- **As mensagens não accusam o plugin indevidamente**: `DescribeFailure`/`DescribeStartFailure` traduzem a exceção real (`ObjectDisposedException` → player reciclado, `TimeoutException`/`HttpRequestException` → Lavalink fora do ar, `TrackException` → detalhe do nó). A menção a `plugins.youtube.enabled` só aparece quando a origem é YouTube **e** a falha veio do nó.
- **Guarda de sessão**: o player é registrado em `_activePlayers` por guilda. Eventos `TrackEnded`/`TrackException`/`TrackStuck` de um player de sessão anterior são ignorados (`Ignorando ... player de sessão anterior`) — sem isso, um evento atrasado derrubava a guilda inteira via `DisconnectAsync`. Se o início falha, a sessão é encerrada e o bot desconecta em vez de ficar mudo.
- Retry limitado a **1 nova tentativa** e só para `ObjectDisposedException` (player reciclado entre o retrieve e o play); qualquer outro erro é reportado, não mascarado.
- `AudioTrackResolver` é puro/síncrono e classifica a origem em `AudioOriginKind` (`Local`, `YouTube`, `YouTubeSearch`, `InstantButton`, `DirectUrl`); só `DirectUrl` vira `RequiresElevatedPermission`. `AudioPlayerService.ResolveAsync` resolve o `InstantButton` via `IInstantSoundResolver` (HTTP + cache de 6h) e troca o identifier pelo MP3.
- O bot guarda o identifier com prefixo no `local:` (`local:sounds/x.mp3`) e converte no envio para o Lavalink (`ToServerIdentifier` → `sounds/x.mp3`), porque o `LocalAudioSourceManager` do Lavalink 4 resolve o caminho literal relativo ao workdir (`/opt/Lavalink`, com sons em `sounds/`).
- YouTube/Spotify: links de página do YouTube passam direto para o Lavalink (o plugin entende `watch`, `youtu.be`, `shorts`, `list`). **Spotify continua sem suporte** — exigiria o plugin `lavasrc` e o resolver `Lavalink4NET.Spotify`.
- Agendamentos persistidos em `Guild.ScheduledSounds` (Mongo) — sobrevivem a restart — e avaliados por `ScheduledSoundService` a cada 30s; só tocam com gente no canal. Guardam a **origem** (`AudioSource`), não os bytes do arquivo. O disparo é **fire-once em sucesso**: se a tentativa do minuto falhar (canal vazio no primeiro tick), o próximo tick tenta de novo; quando toca, a chave fica registrada e não repete no mesmo dia.
- Horários são convertidos para UTC no cadastro e comparados em UTC no disparo; o **dia da semana** é comparado no fuso de Brasília (`ScheduleEvaluator.ShouldFire` converte para `America/Sao_Paulo` antes) — evita o disparo "1 dia antes" em horários noturnos (ex.: `segunda 22:00`). O código usa `DateTime.UtcNow` (nunca o relógio do servidor); o offset de SP é logado no boot: `Fuso de referência America/Sao_Paulo resolvido - offset UTC atual -03:00:00`.
- Sons de entrada/saída (`Guild.JoinVoiceSound`/`LeaveVoiceSound`, Mongo) tocados via `JoinLeaveSoundService` (evento `UserVoiceStateUpdated`), com **cooldown de 15s por usuário** (por tipo — entrar e sair contam separado) e ignorando o próprio bot. O bot **nunca entra no canal só por causa desses sons**: se ele não estiver conectado naquele canal (ou seja, ninguém usou `join`), o som é ignorado. Os demais comandos de áudio continuam com entrada automática (toca-e-sai).
- O modo `join` é monitorado por `PersistentVoiceService`: o bot sai quando o canal fica sem humanos ou é removido; se um admin derrubar o bot do canal, o registro persistente é removido **sem reconectar**. Os locks por guilda do serviço são podados quando ficam ociosos.
