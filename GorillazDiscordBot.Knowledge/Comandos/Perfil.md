---
tags:
  - comandos
  - perfil
atualizado: 2026-09-19
---

# Perfil

[[Comandos|← Comandos]] · [[Comandos/Ensino|Ensino]]

Perfil do usuário e personagens.

## Comandos

| Área | Módulo |
| --- | --- |
| Perfil | `Commands/Profile/ProfileSlashModule.cs` |
| Ensino | `Commands/Profile/EnsinoSlashModule.cs` |

## Detalhes

Personagens e atributos vivem em `Domain/Entity/Profile/CharacterProfile.cs`, persistidos via `ICharacterProfileRepository`. Relaciona-se ao perfil Discord/usuário em `Domain/Entity/Profile/`.