---
tags:
  - ideias
status: ideia
atualizado: 2026-09-24
---

# Extrair o domínio econômico para o serviço Coinflux

[[Home|Home]] · [[Ideias|← Ideias]] · [[Ideias/Como usar|Como usar]]

## Ideia

Extrair o domínio econômico (carteira, banco, loja, inventário, ranking e perfil) do bot para um microserviço irmão, seguindo o padrão definido na ADR-0002. O bot mantém a responsabilidade de identidade (`IPrimaryAccountResolver`).

## Motivação

Permite evoluir a economia com ciclo de vida independente do bot e explorar a topologia sidecar — sem acoplar comandos ao serviço (eles já falam com contratos de domínio).

## Esforço / impacto

- **Esforço:** grande
- **Impacto:** alto

## Notas

- **Dependência:** [[Ideias/Converter os módulos restantes para os contratos de domínio|converter os módulos restantes]] antes da troca de implementação.
- **Topologia:** coinflux como sidecar na mesma task ECS (`localhost:8081`); ECR `coinflux.api:latest`; secrets `coinflux/api-key` e `coinflux/jwt-signing-key`; log group `/ecs/gorillaz-discord-bot/coinflux`.
- **Repo irmão:** `AcougueMineiro/Coinflux` (`Coinflux.sln`, projetos `.Api/.Contracts/.Domain/.Infra/.Tests`; NuGet `Coinflux.Contracts`).
- **Bot:** `Api/Gateways/` passa a implementar `IWalletService`/`IShopService` via HTTP (client `CoinfluxApiClient` no bot).
- Base: [[Decisões/ADR-0002 - Padrão de extração de serviço (Coinflux)|ADR-0002]], [[Decisões/ADR-0001 - Ownership de dados e identidade|ADR-0001]], [[Integrações/LuckyMonkey|padrão de cliente externo]].