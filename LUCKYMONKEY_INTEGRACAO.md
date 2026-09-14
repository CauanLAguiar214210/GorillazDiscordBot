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

- `nuget.config` (raiz do repo): define o feed `github-luckymonkey`
  (`https://nuget.pkg.github.com/CauanLAguiar214210/index.json`) + `nuget.org`.
- As credenciais **não** ficam versionadas: cada dev registra o PAT uma vez no
  config de usuário (ver "Setup do feed por dev" abaixo). No CI, o `GITHUB_TOKEN`
  autentica o restore.
- `GorillazDiscordBot.Api.csproj`: removida a `ProjectReference` para o
  `LuckyMonkey.Contracts` e adicionado:

  ```xml
  <PackageReference Include="LuckyMonkey.Contracts" Version="1.0.0" />
  ```

- `GorillazDiscordBot.Tests` recebe o contrato **transitivamente** via `GorillazDiscordBot.Api`
  (não referencia o pacote diretamente).

### Setup do feed por dev (uma vez por máquina)

O PAT usado para publicar também serve para restaurar. Registre o feed com a
sua credencial no config de usuário (fora do repo):

```sh
dotnet nuget add source "https://nuget.pkg.github.com/CauanLAguiar214210/index.json" \
  --name github-luckymonkey \
  --username CauanLAguiar214210 \
  --password "<PAT>" \
  --store-password-in-clear-text
```

> O `<clear />` do `nuget.config` do repo descarta a *lista* de fontes definida do
> config de usuário, mas as `packageSourceCredentials` de lá são lidas normalmente —
> por isso o segredo fica só no config de usuário.

### Dockerfile

O `dotnet restore` dos estágios `build`/`publish` resolve o pacote
`LuckyMonkey.Contracts` pelo feed do GitHub Packages. O build recebe o token via
`ARG NUGET_AUTH_TOKEN` e registra o feed com a credencial antes do restore:

```dockerfile
ARG NUGET_AUTH_TOKEN
RUN if [ -n "$NUGET_AUTH_TOKEN" ]; then \
        dotnet nuget add source "https://nuget.pkg.github.com/CauanLAguiar214210/index.json" \
            --name github-luckymonkey \
            --username CauanLAguiar214210 \
            --password "$NUGET_AUTH_TOKEN" \
            --store-password-in-clear-text \
            --configfile /root/.nuget/NuGet/NuGet.Config; \
    fi \
    && dotnet restore "GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj"
```

> O `--configfile` aponta para o config de usuário do container — não dá para usar o
> `dotnet nuget add source` sem ele, pois o `github-luckymonkey` já existe no
> `nuget.config` copiado do repo.

- No CI (`aws.yml`), o `docker/build-push-action` passa
  `build-args: NUGET_AUTH_TOKEN=${{ secrets.GITHUB_TOKEN }}`.
- Localmente, informe o token ao build:

  ```sh
  docker build --build-arg NUGET_AUTH_TOKEN="<PAT>" -t gorillaz-discord-bot .
  ```

- O pacote não entra na imagem final (só `/app/publish` é copiado para `base`).

## Resultado

- Bot compila sem nenhuma referência ao repositório do microserviço (`0 avisos, 0 erros`).
- Testes do bot: **206/209** — as 3 falhas restantes são pré-existentes em
  `EconomyFormatTests` (`GorillazDiscordBot.Domain\Entity\Economy\EconomyFormat.cs`
  alterado no working tree, fora do escopo deste trabalho).
- Microserviço: **324/324** testes.
- `LuckyMonkey.Contracts` **1.0.0** publicado no GitHub Packages
  (`nuget.pkg.github.com/CauanLAguiar214210`) e o bot consome pelo feed remoto —
  o nupkg não fica mais versionado dentro do repo.

## Workflow para nova versão do contrato

1. Mudou algo em `LuckyMonkey.Contracts` → subir `Version` no csproj.
2. `dotnet pack LuckyMonkey.Contracts.csproj -c Release`.
3. Publicar no GitHub Packages (requer PAT com `write:packages`):

   ```sh
   dotnet nuget push artifacts/packages/LuckyMonkey.Contracts.<NOVO>.nupkg \
     --source https://nuget.pkg.github.com/CauanLAguiar214210/index.json \
     --api-key "<PAT>"
   ```

4. Atualizar `Version` na `PackageReference` do `GorillazDiscordBot.Api.csproj`.
5. Rebuild + testes no bot.

## Pendências

- (Resolvida) `docker build` do bot — validado com a máquina com Docker ativo.
- (Resolvida) `LuckyMonkey.Contracts` publicado em feed remoto (GitHub Packages);
  o bot aponta para ele e o nupkg deixou de ficar versionado no repo.
- Os 3 testes de `EconomyFormatTests` seguem como falha pré-existente, fora do
  escopo deste trabalho.