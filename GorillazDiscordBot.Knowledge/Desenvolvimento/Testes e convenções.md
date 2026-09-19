---
tags:
  - desenvolvimento
  - testes
atualizado: 2026-09-19
---

# Testes e convenções

[[Desenvolvimento|← Desenvolvimento]] · [[Desenvolvimento/Adicionar um comando|Adicionar um comando]]

## Suíte

- Fraework: **xUnit** (`net9.0`), com **FluentAssertions** e **NSubstitute**.
- Projeto: `GorillazDiscordBot.Tests` (referência Api, Domain e Infra).
- Cobre regras, serviços, repositórios, mapeamentos e módulos sem depender do gateway em execução.

## Rodar

```powershell
dotnet test GorillazDiscordBot.sln
```

## Convenções

- Lógica de negócio no Domain; serviços de aplicação em `Api/Services`; módulos em `Commands/`.
- Contratos de persistência no Domain/Interfaces; implementações na Infra.
- Módulos `[Disabled]` não são registrados no bootstrap.
- Não colocar segredos no código; siga o `.env`/secrets do ambiente (ver [[Operação/Execução local|Execução local]]).