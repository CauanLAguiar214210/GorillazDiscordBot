---
tags:
  - projetos
  - testes
atualizado: 2026-09-19
---

# Tests

[[Projetos|← Projetos]] · [[Projetos/Api|Api]] · [[Projetos/Domain|Domain]] · [[Projetos/Infra|Infra]]

Suíte unitária da solução — **xUnit** (`net9.0`) com **FluentAssertions** e **NSubstitute**; referências a Api, Domain e Infra.

## Cobertura (amostra)

| Área | Arquivos de teste |
| --- | --- |
| Economia/regras | `EconomyRulesTests`, `EconomyAmountParserTests`, `EconomyFormatTests`, `EconomyUnifierTests`, `ClasseEconomiaTests`, `InflationRulesTests`, `CrimeRulesTests` |
| Trabalho/jobs | `JobGameRulesTests`, `JobGameSessionServiceTests`, `JobLicensingTests`, `JobExamSessionServiceTests`, testes por jogo (`CapitaoNavio`, `ComandanteIate`, `CondutorLancha`, `Porteiro`, `Professor`) |
| Cassino | `CasinoTableBuilderTests`, `CasinoPlayServiceTests`, `CasinoJwtProviderTests`, `NewCasinoModulesTests`, `GroupBehaviorTests` |
| Loja/inventário | `ShopServiceTests`, `ShopServiceVehicleTests`, `ShopPetTests`, `ShopUpgradeTests`, `ShopSlashModuleTests` |
| Perfil/veículos | `CharacterProfileTests`, `ProfileSlashModuleTests`, `VehicleRulesTests`, `VehicleSlashModuleTests`, `LicenseRulesTests`, `LicencaExamSessionServiceTests` |
| Módulos | `ConfigSlashModuleTests`, `CrimeSlashModuleTests`, `EnsinoSlashModuleTests`, `InteractionSlashModuleTests`, `ModerationSlashModuleTests`, `TrabalhoSlashModuleTests`, `Release*Tests`, `Manobrista*Tests` |
| Persistência | `MongoMappingsTests`, `SettingsRepositoryTests`, `ReleaseNoteRepositoryTests`, `UserRepositoryTests` |
| Diversos | `MessagePurgeTests`, `PrefixResolverTests`, `ChatInteractionServiceTests`, `PatrimonioServiceTests`, `QuizSessionServiceTests`, `SchoolingQuizRulesTests`, `UserAccountServiceTests`, `ReleaseEmbedBuilderTests` |

## Rodar

```powershell
dotnet test GorillazDiscordBot.sln
```

Convenções detalhadas em [[Desenvolvimento/Testes e convenções|Testes e convenções]].