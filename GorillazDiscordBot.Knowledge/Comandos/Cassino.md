---
tags:
  - comandos
  - cassino
  - luckymonkey
atualizado: 2026-09-19
---

# Cassino

[[Comandos|← Comandos]] · [[Comandos/Economia|Economia]]

Jogos de cassino executados pelo microserviço LuckyMonkey.

## Comandos

| Jogo | Módulo |
| --- | --- |
| Aviãozinho | `Casino/AviaoSlashModule.cs` |
| Baccarat | `Casino/BaccaratSlashModule.cs` |
| Blackjack | `Casino/BlackjackSlashModule.cs` |
| Coin flip | `Casino/CoinFlipSlashModule.cs` |
| Dados | `Casino/DiceSlashModule.cs` |
| High/Low | `Casino/HighLowSlashModule.cs` |
| Limbo | `Casino/LimboSlashModule.cs` |
| Mines | `Casino/MinesSlashModule.cs` |
| Plinko | `Casino/PlinkoSlashModule.cs` |
| Corrida | `Casino/RaceSlashModule.cs` |
| Pedra/papel/tesoura | `Casino/RpsSlashModule.cs` |
| Video poker | `Casino/VideoPokerSlashModule.cs` |
| Roleta (`/roda`) | `Casino/WheelSlashModule.cs` — **`[Disabled]`**, não registrado |

## Detalhes

O fluxo HTTP e autenticação ficam em `Casino/CasinoApiFlow.cs` e `Casino/CasinoSlashModule.cs`; débitos/créditos passam por `PayoutService`. Detalhes do serviço externo em [[Integrações/LuckyMonkey|LuckyMonkey]].