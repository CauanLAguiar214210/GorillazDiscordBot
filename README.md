# GorillazDiscordBot

Discord bot em **.NET 9**, solução com 3 projetos, persistência MongoDB e deploy em AWS ECS/Fargate via Terraform e GitHub Actions.

## Solução

| Projeto | Papel |
|---|---|
| `GorillazDiscordBot.Domain` | Entidades e interfaces de repositórios |
| `GorillazDiscordBot.Infra` | MongoDB, repositórios, serviços externos, configuração (Options pattern) |
| `GorillazDiscordBot.Api` | Host do bot (`DiscordBotService`), módulos de comando |

## Stack

- .NET 9 / C#
- Discord.Net 3.17.4 (Commands + Interactions)
- MongoDB.Driver 3.4.0
- Microsoft.Extensions.Hosting / DI
- AWS.Logger.AspNetCore (CloudWatch, opcional)
- Terraform (ECS/Fargate) · GitHub Actions

## Configuração

Config via `.env` (carregado por DotNetEnv em `GorillazDiscordBot.Api/.env`, template em `.env.example`) ou variáveis de ambiente:

| Variável | Obrigatória | Padrão |
|---|---|---|
| `DISCORD_TOKEN` | Sim | — |
| `MONGODB_CONNECTION_STRING` | Não | `mongodb://localhost:27017` |
| `MONGODB_DATABASE_NAME` | Não | `gorillazbot` |
| `COMMAND_PREFIX` | Não | `macaco ` |
| `OWM_API_KEY` | Não | — |
| `AWS_LOG_GROUP` / `AWS_REGION` | Não | — (ativa logging CloudWatch) |

## Build e execução

```bash
dotnet build GorillazDiscordBot.sln
dotnet run --project GorillazDiscordBot.Api
```

## Arquitetura

- **Comandos**: módulos `ModuleBase<SocketCommandContext>` registrados via reflection (`AddModulesAsync`) e executados por `CommandService`.
- **Prefixo**: `DiscordBotService.HandleCommandAsync` resolve o prefixo por servidor (`GuildPrefixSettings`, fallback para `COMMAND_PREFIX`), com match case-insensitive e fallback de menção (`@bot`).
- **Persistência**: `SettingsRepository<T>` genérico (um documento por servidor, cache em memória, upsert/reset); coleção nomeada por tipo via `MongoMappings` (Bson class maps).
- **GIFs**: entidade com `GuildId`; filtro `Visible(guildId)` = `GuildId == guildId OR GuildId == 0` (globais); `GifUrlService` normaliza URLs (imagem direta ou Tenor via `og:image`/CDN).

## Deploy AWS

- Infra: `GorillazDiscordBot.Infra/AWS` (Terraform), task definition em `task-definition.json`
- Secrets: AWS Secrets Manager (`DISCORD_TOKEN`, `MONGODB_*`, `OWM_API_KEY`)
- CI/CD: GitHub Actions (`.github/workflows/`) — build, push ECR, deploy ECS
