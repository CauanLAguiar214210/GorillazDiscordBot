---
tags:
  - projetos
  - domain
atualizado: 2026-09-24
---

# Domain

[[Projetos|← Projetos]] · [[Projetos/Api|Api]] · [[Projetos/Infra|Infra]] · [[Projetos/Tests|Tests]]

Entidades, regras de negócio, políticas e contratos (persistência + serviços de aplicação) — isolado de frameworks e do MongoDB.

## Estrutura

```text
GorillazDiscordBot.Domain/
├── Entity/
│   ├── Economy/               # EconomyProfile, EconomyRules, CrimeRules, InventoryItem, ShopItem,
│   │                         #   InflationRules, ManobristaRules, EconomyUnifier, EconomiaAmountParser/Format
│   │   └── JobGames/          # jogo do trabalho (Professor, Porteiro, Cozinheiro, Entregador, Faxineiro,
│   │                         #   CapitãoNavio, ComandanteIate, CondutorLancha) + JobGameRules
│   │   ClasseEconomica.cs · EconomyTransaction.cs · JobLicensing.cs · VehicleType.cs
│   ├── Profile/               # CharacterProfile, LicenseRules, SchoolingQuizRules, VehicleRules
│   ├── Ranking/               # RankingTier, HallOfFame
│   ├── Release/               # ReleaseNote · ReleaseSettings
│   └── *.cs                   # DiscordUserProfile, Gif, Guild, GuildInfo, GuildInteraction,
│                              #   GuildInteractionType, GuildMember, PrefixSettings, UserWarning,
│                              #   VoiceChannelSettings, WelcomeSettings
├── Policies/                  # GroupSanctionsPolicy (decisão pura de sanção de alts)
└── Interfaces/               # contratos de persistência + contratos de serviço trocáveis:
    #   I*Repository, IGifUrlService, IGuildSettings, ISettingsRepository,
    #   IPrimaryAccountResolver, IWalletService, IShopService, IAltSanctionPolicy,
    #   ISessionStore<TKey, TSession>
```

## Pontos-chave

- `SettingsRepository<Guild>` guarda settings embutidos por guild (prefixo, boas-vindas, voz).
- Contratos de persistência protegem o domínio dos detalhes do Mongo — implementações em [[Projetos/Infra|Infra]].
- Contratos de serviço econômico (`IPrimaryAccountResolver`, `IWalletService`, `IShopService`, `IAltSanctionPolicy`, `ISessionStore`) são implementados hoje na [[Projetos/Api|Api]] e são os seams da extração do [[Ideias/Extrair o domínio econômico para o serviço Coinflux|Coinflux]] — ver [[Decisões/ADR-0002 - Padrão de extração de serviço (Coinflux)|ADR-0002]].

Ver [[Arquitetura/Backend|Backend]] (domínio e costuras) e [[Arquitetura/Banco de dados|Banco de dados]] (persistência).