---
tags:
  - projetos
  - domain
atualizado: 2026-09-19
---

# Domain

[[Projetos|← Projetos]] · [[Projetos/Api|Api]] · [[Projetos/Infra|Infra]] · [[Projetos/Tests|Tests]]

Entidades, regras de negócio e interfaces de persistência — isolado de frameworks e do MongoDB.

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
└── Interfaces/               # I*Repository, IGifUrlService, IGuildSettings, ISettingsRepository
```

## Pontos-chave

- `SettingsRepository<Guild>` guarda settings embutidos por guild (prefixo, boas-vindas, voz).
- Contratos protegem o domínio dos detalhes do Mongo — implementações em [[Projetos/Infra|Infra]].

Ver [[Arquitetura/Backend|Backend]] (domínio) e [[Arquitetura/Banco de dados|Banco de dados]] (persistência).