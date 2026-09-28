---
tags:
  - decisoes
  - adr
status: aceito
atualizado: 2026-09-24
---

# ADR-0001 - Ownership de dados e identidade

[[Home|Home]] · [[Decisões|← Decisões]]

## Status

`Aceito`

## Contexto

O domínio de economia/gameficação (carteira, banco, loja, cassino, ranking, perfil) está acoplado ao bot: a resolução de identidade (`IEconomyAccessor`, mainId/alt) vivia na Api e os comandos consumiam os repositórios Mongo diretamente, no mesmo banco físico. Para evoluir o domínio para um serviço próprio ([[ADR-0002|Coinflux]]) mantendo a operação atual, é preciso definir quem é dono de cada dado e onde a identidade mora.

## Decisão

- **Identidade = contrato compartilhado no Domain**: `IPrimaryAccountResolver` resolve o mainId (inclui contas vinculadas). Pertence ao lado do bot — serviços externos recebem o mainId via claim JWT (não resolvem alt por conta própria).
- **Ownership de dados:**
  - **Discord / bot:** `Guild`, `GuildMember`, `GuildInteraction`, `Gif`, `ReleaseNote`, `DiscordUserProfile` (o `MainUserId` é do bot).
  - **Coinflux (economia):** `EconomyProfile`, `EconomyTransaction`, `ShopItem`, `InventoryItem`, `RankingTier`, `HallOfFame`, `CharacterProfile`.
- **Anti-goals:** banco físico único não significa posse contratual única; **sem** event bus (costuras in-process); **CD único** (deploy junto com o bot).

**Alternativa descartada:** identidade resolvida pelo próprio serviço (alt por conta própria) — o vínculo é contexto Discord e não pertence ao domínio econômico.

## Consequências

- **Positivas:** contratos compartilhados no Domain (`IPrimaryAccountResolver`, `IWalletService`, `IShopService`, `IAltSanctionPolicy`) permitem trocar a implementação (Mongo → Coinflux HTTP) sem reescrever comandos; políticas de domínio testáveis sem o Discord.
- **Negativas / custos:** a resolução de identidade fica distribuída (bot resolve; serviços herdando via claim); durante a transição boa parte dos comandos ainda consome repositórios diretamente (convertidos por fases — ver ADR-0002).

## Notas

- Código: `GorillazDiscordBot.Domain/Interfaces/IPrimaryAccountResolver.cs`, `IWalletService.cs`, `IShopService.cs`, `IAltSanctionPolicy.cs` e `Policies/GroupSanctionsPolicy.cs`.
- Base compartilhada para a extração definida em [[ADR-0002|ADR-0002 - Padrão de extração de serviço (Coinflux)]].