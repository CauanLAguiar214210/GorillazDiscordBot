---
tags:
  - projetos
  - api
atualizado: 2026-09-19
---

# Api

[[Projetos|← Projetos]] · [[Projetos/Domain|Domain]] · [[Projetos/Infra|Infra]] · [[Projetos/Tests|Tests]]

Camada executável: host do bot, módulos de comando e serviços de aplicação.

## Estrutura

```text
GorillazDiscordBot.Api/
├── Program.cs                 # .env, host genérico, DI, índices, hosted services
├── DiscordBotService.cs       # ciclo de vida Discord e roteamento de eventos
├── Commands/                  # módulos (slashes e prefixados)
│   ├── Casino/                # jogos LuckyMonkey (+ CasinoApiFlow, WheelSlashModule [Disabled])
│   ├── Config/                # Config, Guild, Prefix, Voice
│   ├── Economy/               # Carteira, Banco(+Ativos), Trabalho, Crime
│   ├── Interaction/ Moderation/ Profile/ Release/ Shop/ Vehicle/
│   └── *.cs                   # Account, Economy, Fun, Gif, Help, Interaction, Moderation, Ranking, Shop, Utility
├── Services/                  # aplicação: Shop, Payout, Casino*, Sessões, Releases, Voz, Acesso econômico
├── Utils/                     # CommandCatalog, Guards, DisabledAttribute, TableBuilders, Embeds, Mensageiro
├── Resources/Sounds/          # áudios
└── .env.example               # template de variáveis
```

## Pontos-chave

- `DiscordBotService` registra módulos por reflexão e lida com eventos (mensagens, interações, membros, voz, guild).
- Serviços como singleton no DI; `HttpClient`s dedicados (chat/mídia e LuckyMonkey).
- Serviços hospedados: `DiscordBotService` e `EconomyMaintenanceService`.

Ver [[Arquitetura/Backend|Backend]] para o fluxo completo.