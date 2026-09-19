---
tags:
  - desenvolvimento
  - servicos
atualizado: 2026-09-19
---

# Adicionar um serviço

[[Desenvolvimento|← Desenvolvimento]] · [[Desenvolvimento/Testes e convenções|Testes]]

## Tipos

- **Serviço de aplicação** (ex.: `ShopService`, `PayoutService`) em `GorillazDiscordBot.Api/Services/` — injetado em módulos/outros serviços.
- **Cliente HTTP** (ex.: `CasinoApiClient`) — um `HttpClient` dedicado registrado no `Program.cs`.
- **Serviço hospedado** (ex.: `EconomyMaintenanceService`) — herda `BackgroundService`, agenda de manutenção (ex.: meia-noite UTC) e é registrado como hosted.

## Passos

1. Criar a classe em `Api/Services/` com os contratos de domínio injetados.
2. Registrar no `Program.cs` (singleton por padrão; hosted services em próprio nodo).
3. Testar isolado em `GorillazDiscordBot.Tests/`.

Ver [[Arquitetura/Backend|Backend]] (serviços) e [[Arquitetura/Banco de dados|Banco de dados]] (persistência).