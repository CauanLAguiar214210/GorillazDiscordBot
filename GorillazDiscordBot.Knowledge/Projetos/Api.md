---
tags:
  - projetos
  - api
atualizado: 2026-09-24
---

# Api

[[Projetos|← Projetos]] · [[Projetos/Domain|Domain]] · [[Projetos/Infra|Infra]] · [[Projetos/Tests|Tests]]

Camada executável: host do bot, módulos de comando, serviços de aplicação e as implementações atuais dos contratos econômicos de [[Projetos/Domain|Domain]].

## Estrutura

```text
GorillazDiscordBot.Api/
├── Program.cs                 # .env, host genérico, DI (costuras), índices, hosted services
├── DiscordBotService.cs       # ciclo de vida Discord e roteamento de eventos
├── Commands/                  # módulos (slashes e prefixados)
│   ├── Casino/                # jogos LuckyMonkey (via IWalletService; Sweep/Settle centralizado no CasinoApiFlow)
│   ├── Config/                # Config, Guild, Prefix, Voice
│   ├── Economy/               # Carteira, Banco(+Ativos), Trabalho, Crime
│   ├── Interaction/ Moderation/ Profile/ Release/ Shop/ Vehicle/
│   └── *.cs                   # Account, Economy, Fun, Gif, Help, Interaction, Moderation, Ranking, Shop, Utility
├── Events/                    # GuildEventsSink (usa IAltSanctionPolicy), AutoModService, GuildLogService
├── Services/                  # aplicação + implementações dos contratos de Domain
│   │   #   PayoutService : IWalletService · ShopService : IShopService
│   │   #   PrimaryAccountResolver : IPrimaryAccountResolver · InMemorySessionStore : ISessionStore
│   │   #   CasinoBetTracker, sessões (via ISessionStore), Releases, Voz, CasinoApiClient
│   └── Gateways/              # (planejado) adaptadores HTTP do Coinflux — ver [[Ideias/Extrair o domínio econômico para o serviço Coinflux|ideia]]
├── Utils/                     # CommandCatalog, Guards, DisabledAttribute, TableBuilders, Embeds, Mensageiro
├── Resources/Sounds/          # áudios
└── .env.example               # template de variáveis
```

## Pontos-chave

- `DiscordBotService` registra módulos por reflexão e lida com eventos (mensagens, interações, membros, voz, guild).
- DI das costuras: `IAltSanctionPolicy → GroupSanctionsPolicy`, `IPrimaryAccountResolver → PrimaryAccountResolver`, `IWalletService → PayoutService`, `IShopService → ShopService` (alias da mesma instância).
- Serviços como singleton; `HttpClient`s dedicados (chat/mídia e LuckyMonkey).
- Serviços hospedados: `DiscordBotService` e `EconomyMaintenanceService`.
- Módulos de economia/loja/perfil ainda injetam repositórios/`ShopService` diretamente — conversão planejada em [[Ideias/Converter os módulos restantes para os contratos de domínio|converter os módulos restantes]].

Ver [[Arquitetura/Backend|Backend]] para o fluxo completo.