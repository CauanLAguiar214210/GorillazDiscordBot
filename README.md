# GorillazDiscordBot 🐒

Bot do Discord em **.NET 9** com comandos de prefixo e interações por servidor, persistência em **MongoDB** e deploy em **AWS ECS/Fargate** via Terraform e GitHub Actions.

[![CI](https://github.com/CauanLAguiar214210/GorillazDiscordBot/actions/workflows/ci.yml/badge.svg)](https://github.com/CauanLAguiar214210/GorillazDiscordBot/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Recursos

- 🎮 **Comandos de prefixo** (`macaco <comando>`) e slash commands
- 💰 **Economia**: daily, saldo, apostas, transferências e ranking
- 🖼️ **GIFs**: adicionar, sortear e buscar GIFs (com suporte a Tenor)
- 💬 **Interações personalizadas** por servidor
- 👋 **Boas-vindas e despedidas** configuráveis
- 🔊 **Canais de voz temporários** automáticos
- ⚙️ **Prefixo configurável** por servidor
- 🌐 **Utilidades**: tempo, cotações, F1, 8ball, timer e mais

## Solução

| Projeto | Papel |
|---|---|
| `GorillazDiscordBot.Domain` | Entidades e interfaces de repositórios |
| `GorillazDiscordBot.Infra` | MongoDB, repositórios, serviços externos, configuração (Options pattern) |
| `GorillazDiscordBot.Api` | Host do bot (`DiscordBotService`), módulos de comando |
| `GorillazDiscordBot.Tests` | Testes xUnit (repositórios, prefixo, GIFs e mapeamentos) |

## Stack

- .NET 9 / C#
- Discord.Net 3.17.4 (Commands + Interactions)
- MongoDB.Driver 3.4.0
- Microsoft.Extensions.Hosting / DI
- xUnit · NSubstitute · FluentAssertions
- AWS.Logger.AspNetCore (CloudWatch, opcional)
- Terraform (ECS/Fargate) · GitHub Actions

## Estrutura

```text
GorillazDiscordBot/
├── GorillazDiscordBot.Api/          # Host do bot, comandos e serviços da camada de aplicação
│   ├── Commands/                    # Módulos de comando (módulos ModuleBase)
│   ├── Services/                    # Serviços de interação e canais de voz
│   └── Program.cs                   # DI, options pattern e startup
├── GorillazDiscordBot.Domain/       # Entidades e interfaces (isolado de frameworks)
│   ├── Entity/
│   └── Interfaces/
├── GorillazDiscordBot.Infra/        # Persistência MongoDB, repositórios e infra AWS
│   ├── Configuration/               # Options pattern e Bson class maps
│   ├── Repository/                  # Repositórios genéricos e por entidade
│   ├── Services/                    # Serviços externos (GIFs, etc.)
│   └── AWS/                         # Terraform (networking, ECR, ECS, secrets)
├── GorillazDiscordBot.Tests/        # Testes unitários (xUnit + NSubstitute)
├── .github/workflows/               # CI (build+test) e CD (deploy AWS ECS)
├── Dockerfile
└── docker-compose.yml
```

## Arquitetura

- **Comandos**: módulos `ModuleBase<SocketCommandContext>` registrados via reflection (`AddModulesAsync`) e executados por `CommandService`.
- **Prefixo**: `DiscordBotService.HandleCommandAsync` resolve o prefixo por servidor (`GuildPrefixSettings`, fallback para `COMMAND_PREFIX`), com match case-insensitive e fallback de menção (`@bot`).
- **Persistência**: `SettingsRepository<T>` genérico (um documento por servidor, cache em memória, upsert/reset); coleção nomeada por tipo via `MongoMappings` (Bson class maps).
- **GIFs**: entidade com `GuildId`; filtro `Visible(guildId)` = `GuildId == guildId OR GuildId == 0` (globais); `GifUrlService` normaliza URLs (imagem direta ou Tenor via `og:image`/CDN).
- **Qualidade**: `TreatWarningsAsErrors` + analyzers habilitados via `Directory.Build.props` e `.editorconfig`.

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

## Build, teste e execução

```bash
dotnet restore GorillazDiscordBot.sln
dotnet build GorillazDiscordBot.sln -c Release
dotnet test GorillazDiscordBot.sln          # 27 testes
dotnet run --project GorillazDiscordBot.Api
```

### Docker (MongoDB local + bot)

```bash
# 1. copie o template e preencha o DISCORD_TOKEN
cp GorillazDiscordBot.Api/.env.example GorillazDiscordBot.Api/.env

# 2. suba tudo (MongoDB + bot)
docker compose up --build
```

## Comandos principais

| Comando | Descrição |
|---|---|
| `ajuda` | Lista todos os comandos do bot |
| `daily` / `saldo` / `bet` / `pagar` / `ranking` | Economia |
| `gif <categoria>` | Sorteia um GIF |
| `tempo <cidade>` | Previsão do tempo (OpenWeatherMap) |
| `cotacao` | Cotação de moedas |
| `f1` | Classificação de pilotos de F1 |
| `8ball <pergunta>` | Bola 8 mágica |
| `welcome` / `goodbye` | Configura boas-vindas e despedidas |
| `voice setup` | Criação automática de canais de voz |
| `interaction add <trigger> <resposta>` | Interações personalizadas do servidor |
| `prefix set <novo>` | Altera o prefixo do servidor |

## Deploy AWS

- Infra: `GorillazDiscordBot.Infra/AWS` (Terraform — networking, ECR, ECS e secrets)
- Secrets: AWS Secrets Manager (`DISCORD_TOKEN`, `MONGODB_*`, `OWM_API_KEY`)
- CI/CD: `.github/workflows/` — **ci.yml** (build + testes) e **aws.yml** (build, push ECR, deploy ECS/Fargate)

## Licença

Distribuído sob a licença MIT. Veja [LICENSE](LICENSE).
