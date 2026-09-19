---
tags:
  - comandos
  - economia
atualizado: 2026-09-19
---

# Economia

[[Comandos|← Comandos]] · [[Comandos/Cassino|Cassino]] · [[Comandos/Loja|Loja]]

Módulos de economia e fichas do bot.

## Comandos

| Área | Módulo | Observações |
| --- | --- | --- |
| Carteira | `Commands/Economy/CarteiraSlashModule.cs` | Saldo e transações. |
| Banco | `Commands/Economy/BancoSlashModule.cs` (+ `.Ativos`) | Conta bancária e ativos. |
| Trabalho | `Commands/Economy/TrabalhoSlashModule.cs` | Trabalho com cooldown/sessão. |
| Crime | `Commands/Economy/CrimeSlashModule.cs` | Regras em `Domain/Entity/Economy/CrimeRules.cs`. |
| Prefixados | `Commands/EconomyModule.cs` | Versões legadas via `CommandService`. |

## Detalhes

As regras numéricas (valores, cooldowns, limites) moram no **Domain** (`EconomyRules`, `CrimeRules`) e as transações passam pelo `IEconomyRepository`. Concorrência por usuário é coordenada pelo `ShopService`/caso de uso de economia. Ver [[Arquitetura/Backend|Backend]] para o fluxo e [[Arquitetura/Banco de dados|Banco de dados]] para a persistência.