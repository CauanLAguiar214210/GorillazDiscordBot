# AGENTS.md

Bot Discord (.NET 9) com comandos de prefixo e slash. Documentação detalhada em `GorillazDiscordBot.Knowledge/` (vault Obsidian) — em especial `Arquitetura/` e `Desenvolvimento/`.

## Projetos e dependências

- `GorillazDiscordBot.Api` → `Domain` + `Infra`: host (`DiscordBotService`), módulos e serviços de aplicação.
- `GorillazDiscordBot.Infra` → `Domain`: MongoDB, repositórios, Options pattern, Terraform (`AWS/`).
- `GorillazDiscordBot.Domain`: entidades e contratos, sem dependências de framework.
- `GorillazDiscordBot.Tests`: referencia os três; xUnit + FluentAssertions + NSubstitute, `using Xunit;` global. Não depende do gateway.

## Comandos

- Build: `dotnet build GorillazDiscordBot.sln -c Release`
- Testes: `dotnet test GorillazDiscordBot.sln`
- Teste único: `dotnet test GorillazDiscordBot.sln --filter "FullyQualifiedName~NomeDoTeste"`
- Rodar bot: `dotnet run --project GorillazDiscordBot.Api`
- Local (Mongo + Lavalink + bot): `docker compose up --build`
- Ordem usada no CI: `restore` → `build -c Release --no-restore` → `test -c Release --no-build`

## Gotchas de build

- `TreatWarningsAsErrors=true` (`Directory.Build.props`): qualquer warning **quebra** o build.
- `LuckyMonkey.Contracts` é pacote privado do GitHub Packages (`nuget.config`, feed `github-luckymonkey`). Restore local exige PAT com `read:packages` registrado nas credenciais de usuário (o `<clear/>` do repo apaga a *lista* de fontes, mas as credenciais de usuário continuam valendo). Docker exige o build-arg `NUGET_AUTH_TOKEN`.
- `.env` é carregado de `AppContext.BaseDirectory/.env` (copiado para o output), não da raiz do repo. Template: `GorillazDiscordBot.Api/.env.example`.
- `docker compose` sobe Mongo + Lavalink + bot, mas **não** sobe o LuckyMonkey — localmente o cassino via HTTP fica sem backend.

## Convenções de código

- Regra de negócio no `Domain`; serviços de aplicação em `Api/Services`; módulos em `Api/Commands/<Área>/`.
- Módulos são descobertos por reflexão em `DiscordBotService`: `*SlashModule.cs` (`InteractionModuleBase`) para slash; `*Module.cs` para prefixo. `[Disabled]` impede o registro.
- Prefixo padrão: `macaco ` (com espaço no fim).
- Contratos em `Domain/Interfaces`, implementações em `Infra/Repository`. As implementações dos contratos de **serviço econômico** vivem na Api (seams para o sidecar Coinflux).
- Coleções Mongo nomeadas pelo tipo; registre novas classes BSON em `Infra/Configuration/MongoMappings.cs`. Settings de guild usam `SettingsRepository<T>` (upsert + cache).
- `GorillazDiscordBot.Api` tem `InternalsVisibleTo GorillazDiscordBot.Tests`.

## Deploy e operação

- Branches: push em `dev` roda CI; push em `master` dispara deploy ECS (`aws.yml`, filtrado por paths); PRs miram `master`.
- ECS: uma task com 3 containers (`bot`, `lavalink`:2333 e `luckymonkey`:8080 sidecar) comunicando via `localhost`; segredos no Secrets Manager.
- `LAVALINK_WS_URI` deve terminar em `/v4/websocket` (validado no workflow). Lavalink ≥ 4.2.0 obrigatório (DAVE/E2EE).
- Imagem final Alpine instala `tzdata`/`icu` e usa `TZ=America/Sao_Paulo` — sem `tzdata` o agendamento cai para UTC.
- Terraform em `GorillazDiscordBot.Infra/AWS/`.
- Seeds manuais: `scripts/mongodb/*.js` e `scripts/seed-ranking.js` (rodar com `mongosh` no banco correto).
