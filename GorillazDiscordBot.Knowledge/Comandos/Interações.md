---
tags:
  - comandos
  - interacoes
atualizado: 2026-09-19
---

# Interações

[[Comandos|← Comandos]]

Respostas de chat configuráveis por guild, incluindo mídia (GIFs).

## Comandos

| Módulo | Local |
| --- | --- |
| `InteractionModule` | `Commands/InteractionModule.cs` |
| `InteractionSlashModule` | `Commands/Interaction/InteractionSlashModule.cs` |

## Detalhes

`ChatInteractionService` resolve as respostas por guild; `GifUrlService` normaliza URLs de mídia. Conteúdo persistido em `IGuildInteractionRepository` e cache em memória.