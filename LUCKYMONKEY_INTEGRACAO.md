# Extração do cassino para o microserviço LuckyMonkey

Documento de registro das mudanças feitas para extrair o módulo de cassino do bot
`GorillazDiscordBot` para o microserviço `LuckyMonkey` e remover a dependência de
código do bot em relação ao microserviço.

## Contexto

O cassino foi extraído do bot para o microserviço `LuckyMonkey`
(ASP.NET Core, repo em `E:\Aplications\AcougueMineiro\LuckyMonkey`). O bot passou a
consumir o serviço **somente via HTTP**. Ficou pendente:

- Testes (unitários + integração HTTP) no microserviço.
- Limpeza da domain órfã de jogos que ainda existia no bot.
- Desacoplar o build do bot do código do microserviço.

## Fase 1 — Testes no microserviço (`LuckyMonkey`)

- Criado o projeto `LuckyMonkey.Tests` (xUnit + FluentAssertions + NSubstitute +
  `Microsoft.AspNetCore.Mvc.Testing` + coverlet), referenciando `LuckyMonkey.Api` e
  `LuckyMonkey.Contracts`, adicionado à `LuckyMonkey.sln`.
- `LuckyMonkey.Api`:

  - `<InternalsVisibleTo Include="LuckyMonkey.Tests" />` no csproj;
  - `public partial class Program { }` necessário para `WebApplicationFactory<Program>`.

- Testes unitários (306, todos verdes em `-c Release`):

  - 17 jogos portados do bot (`LuckyMonkey.Tests\Games\`);
  - sessões e ledger (`SessionStoreTests`, `BetLedgerTests`);
  - mapeamento de estado (`StateMapperTests`);
  - orquestração (`CasinoOrchestratorTests` — 23 testes).

- Testes de integração HTTP (`CasinoApiTests`, 18 testes verdes): health, autenticação
  (API key + JWT), fluxos de slots/roleta/replay, expiração de sessão, e mapeamento de
  erros. Helper injeta as 4 chaves de config via `builder.UseSetting(...)` — e **não**
  via `ConfigureAppConfiguration`, que no minimal hosting não alcança o
  `builder.Configuration` do `Program`.

**Resultado: 324 testes aprovados, 0 falhas.**

## Fase 2 — Limpeza da domain órfã no bot

- Removidos os 18 arquivos de `GorillazDiscordBot.Domain\Entity\Games\`
  (15 jogos em `Casino\` + `Card.cs`, `Deck.cs`, `BlackjackGame.cs`).
- Removidos os 17 testes órfãos correspondentes em `GorillazDiscordBot.Tests`
  (arquivos `*GameTests.cs`, `DeckTests.cs`, `CasinoRulesTests.cs`).
- Mantidos `NewCasinoModulesTests.cs`, `CasinoPlayServiceTests.cs`, `CasinoTableBuilderTests.cs`,
  `CasinoJwtProviderTests.cs` e os testes não-cassino.

## Fase 3 — Contrato do microserviço via NuGet (desacoplamento do build)

O bot precisa dos DTOs/enums do contrato (ex.: `BetResponse`, `RouletteState`,
`GameKind`, `CardDto`) para deserializar as respostas HTTP. Antes isso era feito por
`ProjectReference` apontando para o repositório do microserviço, o que acoplava
o build do bot ao código e ao layout do outro repo. A solução adotada foi empacotar
o contrato como NuGet.

### No microserviço (`LuckyMonkey.Contracts`)

- Adicionado ao `LuckyMonkey.Contracts.csproj`:

  - `PackageId=LuckyMonkey.Contracts`;
  - `Version=1.0.0`;
  - `Description` e `Authors`;
  - `PackageOutputPath=$(MSBuildThisFileDirectory)artifacts\packages`.

- Gerar o pacote:

  ```sh
  dotnet pack LuckyMonkey.Contracts.csproj -c Release
  ```

  Produz `artifacts\packages\LuckyMonkey.Contracts.1.0.0.nupkg`.

### No bot (`GorillazDiscordBot`)

- `nuget.config` (raiz do repo): define o feed local `packages` (caminho relativo ao
  próprio repo) + `nuget.org`.
- `packages\LuckyMonkey.Contracts.1.0.0.nupkg`: pacote versionado dentro do repo,
  exceção adicionada no `.gitignore` para o nupkg não ser ignorado.
- `GorillazDiscordBot.Api.csproj`: removida a `ProjectReference` para o
  `LuckyMonkey.Contracts` e adicionado:

  ```xml
  <PackageReference Include="LuckyMonkey.Contracts" Version="1.0.0" />
  ```

- `GorillazDiscordBot.Tests` recebe o contrato **transitivamente** via `GorillazDiscordBot.Api`
  (não referencia o pacote diretamente).

### Dockerfile

Verificação: atualização **necessária** — o `dotnet restore` dos estágios `build`/`publish`
precisa resolver o pacote `LuckyMonkey.Contracts`, e o feed/local estava fora do build context.

Alterado para copiar `nuget.config` e `packages/` **antes** do `RUN dotnet restore`:

```dockerfile
COPY nuget.config ./
COPY packages ./packages

RUN dotnet restore "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj"
```

- `nuget.config` vai para `/src/nuget.config` (restore do csproj em
  `/src/GorillazDiscordBot.Api/` o encontra subindo na árvore).
- `packages` relativo resolve para `/src/packages`, onde o nupkg foi copiado.
- O `COPY . .` posterior re-copia esses arquivos sem efeito colateral.
- O pacote não entra na imagem final (só `/app/publish` é copiado para `base`).

**Pendente por ambiente local: rodar `docker build` de verdade (daemon Docker Desktop
estava offline na validação).**

## Resultado

- Bot compila sem nenhuma referência ao repositório do microserviço (`0 avisos, 0 erros`).
- Testes do bot: **206/209** — as 3 falhas restantes são pré-existentes em
  `EconomyFormatTests` (`GorillazDiscordBot.Domain\Entity\Economy\EconomyFormat.cs`
  alterado no working tree, fora do escopo deste trabalho).
- Microserviço: **324/324** testes.

## Workflow para nova versão do contrato

1. Mudou algo em `LuckyMonkey.Contracts` → subir `Version` no csproj.
2. `dotnet pack LuckyMonkey.Contracts.csproj -c Release`.
3. Copiar o novo `.nupkg` para `packages\` do bot (remover a versão antiga).
4. Atualizar `Version` na `PackageReference` do `GorillazDiscordBot.Api.csproj`.
5. Rebuild + testes no bot.

## Pendências

- Rodar `docker build` do bot com a máquina com Docker ativo.
- (Recomendado p/ CI/AWS) Publicar `LuckyMonkey.Contracts` em um feed remoto
  (GitHub Packages / Artifactory / ECR seguro) e apontar o bot para ele — aí o nupkg
  deixa de precisar ficar versionado dentro do repo.
- Resolver (se desejado) as 3 falhas de `EconomyFormatTests`, que são pré-existentes
  e não relacionadas a esta tarefa.