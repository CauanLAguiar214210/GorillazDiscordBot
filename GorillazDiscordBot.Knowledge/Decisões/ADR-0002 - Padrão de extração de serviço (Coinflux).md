---
tags:
  - decisoes
  - adr
status: aceito
atualizado: 2026-09-24
---

# ADR-0002 - Padrão de extração de serviço (Coinflux)

[[Home|Home]] · [[Decisões|← Decisões]]

## Status

`Aceito`

## Contexto

O domínio de economia é o maior acoplamento do bot (repositórios Mongo usados diretamente pelos comandos, `ResolveMainIdAsync` repetido dezenas de vezes, CRUD de settings copiado em módulos). Separar em um serviço de uma vez é inviável; adotamos costuras incrementais que, juntas, deixam o domínio trocável. Segue o padrão já validado com o microserviço de cassino (sidecar LuckyMonkey).

## Decisão

- **Serviço irmão `Coinflux`** (repo `AcougueMineiro/Coinflux`): solução `Coinflux.sln`, projetos `Coinflux.Api`, `Coinflux.Contracts`, `Coinflux.Domain`, `Coinflux.Infra`, `Coinflux.Tests`; contrato via NuGet `Coinflux.Contracts`.
- **Topologia cloud:** sidecar na **mesma task ECS** do bot (padrão LuckyMonkey) — imagem ECR `coinflux.api:latest`, container `coinflux` em `localhost:8081`, env `COINFLUX_URL` / `COINFLUX_API_KEY` / `COINFLUX_JWT_SIGNING_KEY`, secrets `coinflux/api-key` e `coinflux/jwt-signing-key`, log group `/ecs/gorillaz-discord-bot/coinflux`; cliente no bot `CoinfluxApiClient` (X-Api-Key + JWT).
- **Primeiro corte extraído:** Economia + Shop.
- **Costuras executadas na Fase A (comportamento preservado):**
  - `IEconomyAccessor` → **`IPrimaryAccountResolver`** (interface no Domain; implementação `PrimaryAccountResolver` na Api).
  - **`IWalletService`** no Domain; `PayoutService` implementa e é registrado como `IWalletService`; os fluxos de cassino (15 módulos + `CasinoApiFlow`) passam a depender da interface; `PayoutResult`/`AppliedRelic` movidos para o Domain.
  - **`IShopService`** no Domain; `ShopService` implementa; `PayoutService` e `PatrimonioService` dependem da interface (registrada como alias **da mesma instância** — preserva lock-pool e cache de catálogo).
  - **`IAltSanctionPolicy`** + `GroupSanctionsPolicy` no Domain (decisão pura de sanção de alts); o sink `GuildEventsSink` continua aplicando ban/timeout no Discord.
  - Adaptadores futuros em `GorillazDiscordBot.Api/Gateways/` (implementação HTTP à frente do Mongo).
- **Costuras executadas na Fase B (comportamento preservado):**
  - **`ISessionStore<TKey, TSession>`** no Domain + `InMemorySessionStore` na Api — as 5 sessões efêmeras (`QuizSessionService`, `JobExamSessionService`, `LicencaExamSessionService`, `ManobristaSessionService`, `JobGameSessionService`) passam a armazenar via o contrato (ctors públicos intactos); permite trocar o armazenamento (Redis/Coinflux) sem tocar na lógica.
  - **`SettleExpiredAsync` centralizado no `CasinoApiFlow`** (retorno automático de partida expirada): os 14 métodos privados duplicados nos módulos viram delegados de uma linha; descrições de transação preservadas por mapa de `GameKind`.

## Consequências

- **Positivas:** o fluxo do dinheiro (aposta/pagamento/relíquia) e a identidade já passam por contratos de domínio — os módulos de cassino podem ser apontados ao Coinflux sem reescrita; a política de alts é pura e testável; sem mudança de comportamento (518/518 testes, 0 avisos no build).
- **Negativas / custos:** conversão dos módulos de comando de loja/economia restantes (Works, Crime, Carteira, Banco, Shop, Profile) para as interfaces fica para fases seguintes — seguem consumindo repositórios diretamente (ainda contratos de domínio); dependência do Mongo nessas superfícies permanece durante a transição.

## Notas

- Depende de [[ADR-0001|ADR-0001 - Ownership de dados e identidade]] (quem é dono de cada coleção e onde a identidade mora).
- Referência de integração do padrão atual: [[Integrações/LuckyMonkey|LuckyMonkey]].