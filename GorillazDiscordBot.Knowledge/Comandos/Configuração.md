---
tags:
  - comandos
  - configuracao
atualizado: 2026-09-19
---

# Configuração

[[Comandos|← Comandos]]

Preferências por guild: prefixo, boas-vindas, voz e configurações gerais.

## Comandos

| Área | Módulo |
| --- | --- |
| Configuração | `Commands/Config/ConfigSlashModule.cs` |
| Guild | `Commands/Config/GuildModule.cs` |
| Prefixo | `Commands/Config/PrefixModule.cs` |
| Voz | `Commands/Config/VoiceModule.cs` |

## Detalhes

Persistência via `ISettingsRepository<Guild>` com cache concorrente por guild e *upsert* — ver [[Arquitetura/Banco de dados|Banco de dados]]. O prefixo cai para `COMMAND_PREFIX` quando a guild não configurou.