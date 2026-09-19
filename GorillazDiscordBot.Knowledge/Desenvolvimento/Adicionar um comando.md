---
tags:
  - desenvolvimento
  - comandos
atualizado: 2026-09-19
---

# Adicionar um comando

[[Desenvolvimento|← Desenvolvimento]] · [[Desenvolvimento/Testes e convenções|Testes]]

## Passos

1. Criar o módulo em `GorillazDiscordBot.Api/Commands/` (subpasta por área).
   - **Slash:** herdar o módulo de interações (ex.: `*SlashModule.cs`).
   - **Prefixado:** herdar o módulo do `CommandService` (ex.: `*Module.cs`).
2. Injetar os serviços necessários pelo construtor (DI resolve como singleton).
3. O bootstrap descobre o módulo por reflexão em `DiscordBotService`; use `[Disabled]` para não registrar.
4. Adicionar os testes em `GorillazDiscordBot.Tests/` (ver [[Desenvolvimento/Testes e convenções|Testes e convenções]]).
5. Se for comando de cassino, seguir o fluxo de [[Integrações/LuckyMonkey|LuckyMonkey]].

## Regras

- Manter a lógica de negócio nos serviços/domínio — o módulo só adapta a interação.
- Comandos que usam sessão em memória (quiz, trabalho, manobrista, apostas) não sobrevivem a reinício.

Ver [[Arquitetura/Backend|Backend]] para o fluxo de execução.