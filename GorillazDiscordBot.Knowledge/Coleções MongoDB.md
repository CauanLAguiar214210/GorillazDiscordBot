---
tags:
  - banco-de-dados
  - mongodb
atualizado: 2026-09-19
---

# Coleções MongoDB

[[Home|Home]] · [[Arquitetura/Banco de dados|Banco de dados]]

As coleções são nomeadas **pelo nome do tipo** (`typeof(T).Name` em `MongoRepository` / `nameof` nos repositórios especializados). Mapeamentos BSON/`ObjectId` centralizados em `GorillazDiscordBot.Infra/Configuration/MongoMappings.cs`.

## Coleções (databse padrão `gorillazbot`)

| Coleção | Tipo mapeado | Observações |
| --- | --- | --- |
| `EconomyProfile` | `EconomyProfile` | Saldo, banco, poupança, boosts; campos `Money`, `Bank`, `Savings*`, `*Boost*`, `RobShieldUntil` etc. |
| `EconomyTransaction` | `EconomyTransaction` | Auditoria: `Type`, `Amount`, `Description`, `CreatedAt`. |
| `DiscordUserProfile` | `DiscordUserProfile` | Usuário; `MainUserId` para [[Glossário/Economias vinculadas|contas vinculadas]]. |
| `CharacterProfile` | `CharacterProfile` | Perfil/personagem: escolaridade, licenças, diplomas, equipados (`*Key`) — índice único `uq_Profile_UserId`. |
| `ShopItem` | `ShopItem` | Catálogo da [[Comandos/Loja|loja]]: `Key`, `Price`, `Category`, efeitos/upgrades/veículos/armas. |
| `InventoryItem` | `InventoryItem` | Itens do usuário: `ItemKey`, `Quantity`, `ExpiresAt`, `IsEquipped`. |
| `RankingTier` | `RankingTier` | Tiers por `MinNetWorth` ([[Glossário/Tier|Tier]]). |
| `HallOfFame` | `HallOfFame` | [[Glossário/Hall da fama|Hall da fama]]: `Phase`, `Title`, `Phrase`. |
| `Guild` | `Guild` | Settings de guild embutidos (prefixo, boas-vindas, voz) via `SettingsRepository<T>` (upsert). |
| `GuildMember` | `GuildMember` | Membro em guild: alertas (`Warnings`), mute, ban. |
| `GuildInteraction` | `GuildInteraction` | Respostas de chat por guild (`trigger`, `tipo`, `response`). |
| `Gif` | `Gif` | Catálogo de GIFs (`nome`, `url`, `categoria`). |
| `ReleaseNote` | `ReleaseNote` | Notas de release: `Version`, `Title`, `Features`. |

## Índices garantidos no boot

`Program.cs` chama `EnsureIndexesAsync` para membros, usuários, loja, ranking, personagens e releases. Ex.: `uq_Profile_UserId` (único) em `CharacterProfile`.

## Seeds e manutenção

Scripts em `scripts/mongodb/` (catálogo de loja, releases, veículos, armas/equipamentos/pets, normalizações) e `scripts/seed-ranking.js` — ver [[Operação/Execução local|Execução local]].