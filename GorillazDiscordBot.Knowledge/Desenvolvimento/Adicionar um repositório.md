---
tags:
  - desenvolvimento
  - infra
atualizado: 2026-09-19
---

# Adicionar um repositório

[[Desenvolvimento|← Desenvolvimento]] · [[Desenvolvimento/Testes e convenções|Testes]]

## Passos

1. **Contrato** no `GorillazDiscordBot.Domain/Interfaces/` (ex.: `IXyzRepository`).
2. **Implementação** no `GorillazDiscordBot.Infra/Repository/` herdando `MongoRepository<T>` (ou `SettingsRepository<T>` para config de guild).
3. **Mapeamentos** BSON/`ObjectId` em `GorillazDiscordBot.Infra/Configuration/MongoMappings.cs`.
4. **DI** no `Program.cs`; adicionar `EnsureIndexesAsync` no boot se a coleção precisar de índices.
5. Cobertura em `GorillazDiscordBot.Tests/` (repositório + mapeamentos).

Ver [[Arquitetura/Banco de dados|Banco de dados]] para o modelo de acesso.