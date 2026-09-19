---
tags:
  - comandos
  - releases
atualizado: 2026-09-19
---

# Releases

[[Comandos|← Comandos]]

Publicação de releases no servidor.

## Comandos

| Módulo | Local |
| --- | --- |
| `ReleaseSlashModule` | `Commands/Release/ReleaseSlashModule.cs` |

## Detalhes

`ReleaseAnnouncementService` publica releases pendentes nas guilds; `ReleaseEmbedBuilder` monta os embeds; notas persistidas via `IReleaseNoteRepository`. Scripts de seed em `scripts/mongodb/seed-releases*.js`.