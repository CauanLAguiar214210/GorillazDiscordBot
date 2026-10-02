# GorillazDiscordBot 🐒

Bot do Discord em **.NET 9** com comandos de prefixo e interações por servidor, persistência em **MongoDB** e deploy em **AWS ECS/Fargate** via Terraform e GitHub Actions.

[![CI](https://github.com/CauanLAguiar214210/GorillazDiscordBot/actions/workflows/ci.yml/badge.svg)](https://github.com/CauanLAguiar214210/GorillazDiscordBot/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/9.0)

## Recursos

- 🎮 **Comandos de prefixo** (`macaco <comando>`) e slash commands
- 💰 **Economia**: daily, saldo, transferências, poupança e ranking
- 🎰 **Cassino**: blackjack, roleta (número, cor, par/ímpar, metade) e caça-níquel com multiplicadores
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
- **Prefixo**: `DiscordBotService.HandleCommandAsync` resolve o prefixo por servidor (`Guild.Prefix`, fallback para `COMMAND_PREFIX`), com match case-insensitive e fallback de menção (`@bot`).
- **Persistência**: `SettingsRepository<T>` genérico (um documento `Guild` por servidor com settings embutidos — prefix/welcome/voice — e metadados do servidor em `GuildInfo`; cache em memória, upsert/reset); coleção nomeada por tipo via `MongoMappings` (Bson class maps).
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
| `LUCKY_MONKEY_URL` | Não | `http://localhost:8080` |
| `LUCKY_MONKEY_API_KEY` | Não | — (header `X-Api-Key` do cassino) |
| `LUCKY_MONKEY_JWT_SIGNING_KEY` | Não | — (deve ser a mesma `LuckyMonkey:Jwt:SigningKey` do serviço de cassino) |
| `LUCKY_MONKEY_JWT_ISSUER` / `LUCKY_MONKEY_JWT_AUDIENCE` | Não | `LuckyMonkey` / `LuckyMonkey.Clients` |
| `AUDIO_INSTANT_MIRROR_BASE_URL` | Não | `https://myinstants.site` (espelho para links de som instantâneo) |
| `AUDIO_PLAYBACK_START_TIMEOUT_SECONDS` | Nao | `15` (segundos ate o Lavalink confirmar `TrackStarted`) |
| `AUDIO_UPLOAD_PATH` | Não | `/audio/uploads` (mesmo volume montado em `/opt/Lavalink/sounds/uploads`) |
| `AUDIO_UPLOAD_MAX_MB` | Não | `25` (tamanho máximo de um anexo favoritado) |
| `AUDIO_UPLOAD_MAX_MINUTES` | Não | `10` (duração máxima de um anexo favoritado) |
| `AUDIO_UPLOAD_RETENTION_DAYS` | Não | `30` (idade máxima de um upload no disco) |
| `YOUTUBE_POT_TOKEN` / `YOUTUBE_VISITOR_DATA` | Não | Tokens anti-bot do `youtube-source`; necessários quando o YouTube exigir login |
| `YOUTUBE_OAUTH_ENABLED` | Não | `false`; habilite o fluxo OAuth do `youtube-source` |
| `YOUTUBE_OAUTH_REFRESH_TOKEN` | Não | Refresh token gerado pelo fluxo OAuth, quando disponível |
| `AWS_LOG_GROUP` / `AWS_REGION` | Não | — (ativa logging CloudWatch) |

## Build, teste e execução

```bash
dotnet restore GorillazDiscordBot.sln
dotnet build GorillazDiscordBot.sln -c Release
dotnet test GorillazDiscordBot.sln          # 707 testes
dotnet run --project GorillazDiscordBot.Api
```

### Docker (MongoDB local + bot)

```bash
# 1. copie o template e preencha o DISCORD_TOKEN
cp GorillazDiscordBot.Api/.env.example GorillazDiscordBot.Api/.env

# 2. forneça um PAT clássico do GitHub para LuckyMonkey.Contracts
#    O token precisa de `read:packages` e acesso ao pacote privado.
$env:NUGET_AUTH_TOKEN = "ghp_..."       # PowerShell
# export NUGET_AUTH_TOKEN="ghp_..."     # Bash

# 3. suba tudo (MongoDB + Lavalink + bot)
docker compose up --build
```

`NUGET_AUTH_TOKEN` é usado somente como argumento do build para restaurar o pacote
privado `LuckyMonkey.Contracts`; ele não é injetado no container em runtime. No
PowerShell, defina a variável na mesma sessão em que executar `docker compose up`.
O token precisa ter `read:packages` e acesso de leitura a
`CauanLAguiar214210/LuckyMonkey.Contracts`.

Se o Lavalink registrar `Sign in to confirm you're not a bot`, configure um
`poToken` e o respectivo `visitorData` do YouTube na sessão que executa o
Compose. O plugin `youtube-source` documenta a geração desses valores em
`youtube-trusted-session-generator`:

```powershell
$env:YOUTUBE_POT_TOKEN = "seu_po_token"
$env:YOUTUBE_VISITOR_DATA = "seu_visitor_data"
docker compose up -d --build lavalink
```

Essas variáveis precisam estar definidas no shell que executa o Compose (ou no
arquivo `.env` da raiz do projeto). Colocá-las apenas em
`GorillazDiscordBot.Api/.env` não as envia para o container do Lavalink, pois
esse arquivo é usado pelo serviço do bot em runtime.

Esses valores não devem ser commitados nem colocados no `.env.example`.

O gerador acima está atualmente depreciado e pode falhar. Alternativamente,
use OAuth com uma conta secundária do YouTube (não use sua conta principal):

```powershell
$env:YOUTUBE_OAUTH_ENABLED = "true"
docker compose up -d --build --force-recreate lavalink
docker logs -f gorillaz-lavalink
```

O Lavalink exibirá as instruções/código para autorizar a conta. Depois da
autorização, copie o `refreshToken` exibido para
`YOUTUBE_OAUTH_REFRESH_TOKEN` e recrie o serviço. OAuth e `poToken` não devem
ser usados simultaneamente.

O compose cria o volume `audio_uploads` e monta o mesmo conteúdo no bot
(`/audio/uploads`) e no Lavalink (`/opt/Lavalink/sounds/uploads`). Não remova esse
volume enquanto houver favoritos baseados em anexos. A limpeza automática remove
arquivos que ultrapassarem `AUDIO_UPLOAD_RETENTION_DAYS`, mesmo se ainda estiverem
referenciados por um favorito.

Para subir uma imagem já construída, sem repetir o restore:

```bash
docker compose up -d --no-build
```

## Comandos principais

| Comando | Descrição |
|---|---|
| `ajuda` | Lista todos os comandos do bot |
| `daily` / `saldo` / `pagar` / `ranking` | Economia |
| `blackjack <valor>` / `roleta <valor>` / `cacaniquel <valor>` | Cassino (blackjack, roleta e caça-níquel) |
| `loja` / `comprar <id>` / `inventario` / `usar <id>` / `vender <id>` | Loja com itens e boosts (catálogo no banco) |
| `equipar <id>` / `desequipar <id>` | Equipa relógios com bônus permanente no cassino |
| Ativos (Ações, Fazenda, Terreno, Empresa) | Renda passiva no `daily` (backlog máx. 3 dias) |
| Relógios (Relógio do Cassino, da Sorte, Vermelho, do Reembolso) | Itens caros equipáveis: +% nos ganhos (todos ou jogo específico) ou cashback % nas derrotas |
| `gif <categoria>` | Sorteia um GIF |
| `8ball <pergunta>` | Bola 8 mágica |
| `welcome` / `goodbye` | Configura boas-vindas e despedidas |
| `voice setup` | Criação automática de canais de voz |
| `interaction add <trigger> <resposta>` | Interações personalizadas do servidor |
| `tocar <origem>` / `parar` / `sons` | Áudio no canal de voz — YouTube, busca (`!termo`), Myinstants, URL de áudio ou `local:arquivo` |
| `favoritar <origem> = <apelido>` / `favoritar-anexo` / `favs` / `tocarfav <chave>` / `desfavoritar <chave>` | Favoritos de áudio da guilda (até 100, com apelido opcional; anexos vão para o volume compartilhado com o Lavalink) |
| `prefix set <novo>` | Altera o prefixo do servidor |

## Deploy AWS

- Infra: `GorillazDiscordBot.Infra/AWS` (Terraform — networking, ECR, ECS e secrets)
- Secrets: AWS Secrets Manager (`DISCORD_TOKEN`, `MONGODB_*`, `OWM_API_KEY`)
- CI/CD: `.github/workflows/` — **ci.yml** (build + testes) e **aws.yml** (build, push ECR, deploy ECS/Fargate)

## Licença

Distribuído sob a licença MIT. Veja [LICENSE](LICENSE).
