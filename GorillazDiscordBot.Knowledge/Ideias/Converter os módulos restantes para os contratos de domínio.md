---
tags:
  - ideias
status: priorizada
atualizado: 2026-09-24
---

# Converter os módulos restantes para os contratos de domínio

[[Home|Home]] · [[Ideias|← Ideias]] · [[Ideias/Como usar|Como usar]]

## Ideia

Converter os módulos de comando de economia que ainda injetam `IEconomyRepository`, `ShopService` e `IPrimaryAccountResolver` para dependerem **apenas** dos contratos de domínio: `IWalletService`, `IShopService`, `IPrimaryAccountResolver` e `IPatrimonioService`.

## Motivação

É o desdobramento natural das Fases A/B já executadas (cassino, sessões e convites já convergidos). Quando o caminho de comando só enxergar contratos, a troca para o serviço Coinflux vira decisão puramente de DI — sem reescrita de comandos.

## Esforço / impacto

- **Esforço:** médio
- **Impacto:** alto (habilita a extração do Coinflux)

## Notas

- Módulos alvo: `CarteiraSlashModule`, `BancoSlashModule` (+`Ativos`), `ShopSlashModule`, `ProfileSlashModule`, `TrabalhoSlashModule`, `CrimeSlashModule`, `RankingModule` e os legacy `EconomyModule`/`ShopModule`.
- Escopo em aberto (decisões pendentes):
  1. **Amplitude:** converter só os módulos, ou já criar `Api/Gateways/` nesta rodada?
  2. **Legacy (`EconomyModule`/`ShopModule`):** converter para as interfaces ou aposentar os comandos prefixados?
  3. **Banco × Carteira:** manter depósito/saque/poupança em `IWalletService`, ou criar `IBankService` separado?
- Validar com `dotnet build` 0 avisos e `dotnet test` (suíte atual em 518/518).
- Base: [[Decisões/ADR-0002 - Padrão de extração de serviço (Coinflux)|ADR-0002]].