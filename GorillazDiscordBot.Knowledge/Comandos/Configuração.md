---
tags:
  - comandos
  - configuracao
atualizado: 2026-09-27
---

# Configuração

[[Comandos|← Comandos]]

Preferências por guild: prefixo, boas-vindas, despedidas, novidades, voz e configurações gerais.

## Comandos

| Área | Módulo |
| --- | --- |
| Configuração (`/config`) | `Commands/Config/ConfigSlashModule.cs` |
| Cooldowns (`/cooldown`) | `Commands/Moderation/CooldownSlashModule.cs` |
| Permissões (`/permissao`) | `Commands/Moderation/PermissionSlashModule.cs` |
| Log de auditoria (`/modlog`) | `Commands/Moderation/ModLogSlashModule.cs` |
| Auto-moderação (`/automod`) | `Commands/Moderation/AutoModSlashModule.cs` |
| Anti-raid (`/anti-raid`) | `Commands/Moderation/AntiRaidSlashModule.cs` |

## Detalhes

Persistência via `ISettingsRepository<Guild>` com cache concorrente por guild e *upsert* — ver [[Arquitetura/Banco de dados|Banco de dados]]. O prefixo cai para `COMMAND_PREFIX` quando a guild não configurou (`Utils/PrefixResolver.cs`).

Cooldowns e permissões por cargo são aplicados em tempo de execução pelo `Services/CommandPolicyService.cs`, no dispatch de comandos de prefixo e de interações (admins/owner são isentos). Cofigurações de proteção (invites, @everyone/@here, menções, strikes 3/6 e limites de entrada) ficam em `/automod` e `/anti-raid`.