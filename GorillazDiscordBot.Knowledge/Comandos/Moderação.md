---
tags:
  - comandos
  - moderacao
atualizado: 2026-09-27
---

# Moderação

[[Comandos|← Comandos]]

Ações administrativas da guild, aplicação de punições automáticas por avisos e log de auditoria.

## Comandos

| Área | Módulo |
| --- | --- |
| Ações (limpar/banir/timeout/avisos/slowmode/trancar) | `Commands/Moderation/ModerationModule.cs` (prefixo) e `Commands/Moderation/ModerationSlashModule.cs` (slash) |
| Log de auditoria (`/modlog`) | `Commands/Moderation/ModLogSlashModule.cs` |
| Auto-moderação (`/automod`) | `Commands/Moderation/AutoModSlashModule.cs` |
| Cooldowns (`/cooldown`) | `Commands/Moderation/CooldownSlashModule.cs` |
| Permissões (`/permissao`) | `Commands/Moderation/PermissionSlashModule.cs` |
| Anti-raid (`/anti-raid`) | `Commands/Moderation/AntiRaidSlashModule.cs` |

## Detalhes

- **Avisos**: persistidos por guild, listados com id sequencial e removíveis (`/removeaviso`). Ação por `Services/StrikeEnforcementService.cs`: timeout quando os avisos atingem o limite configurado (padrão 3) e ban a partir de outro limite (padrão 6) — desligado até ativar em `/automod strikes`.
- **Log de auditoria**: eventos do servidor consolidados por `Services/GuildLogService.cs` (via `IBotEventSink`), embeds em `Utils/GuildLogEmbeds.cs`; canal e eventos configuráveis em `/modlog`. Em geral desligado por padrão para voz e alteração de canais.
- **Anti-raid**: `Services/RaidGuardService.cs` bane entradas recentes quando o servidor ultrapassa o limite por janela (`/anti-raid`), registrando o alerta no log.
- **Infrações** (banir/expulsar/timeout/avisar) também enviam DM ao membro via `Services/DmNotifier.cs`.