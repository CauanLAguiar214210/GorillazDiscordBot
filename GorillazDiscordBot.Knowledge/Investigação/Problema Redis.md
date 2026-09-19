---
tags:
  - investigacao
  - redis
status: sem uso identificado no bot
atualizado: 2026-09-18
---
# Problema Redis

[[Home|Home]] · [[Investigação|← Investigação]] · [[Arquitetura/Banco de dados|Banco de dados]]

## Sintoma

Não há sintoma nem erro Redis disponível no repositório. A análise estática não encontrou uso de Redis pelo GorillazDiscordBot.

## Impacto

Não existe um componente Redis do bot que possa ser diagnosticado ou corrigido a partir deste código. Um eventual erro Redis pode pertencer a outro serviço, como a imagem externa do LuckyMonkey.

## Evidências

- Não há referências a `Redis`, `StackExchange.Redis`, `IDistributedCache`, `ConnectionMultiplexer` ou cache distribuído no código, arquivos de projeto, Docker, ECS, Terraform ou documentação.
- `GorillazDiscordBot.Api/GorillazDiscordBot.Api.csproj:13-27` e `GorillazDiscordBot.Infra/GorillazDiscordBot.Infra.csproj:10-14` não declaram dependências Redis.
- `GorillazDiscordBot.Api/Program.cs:18-35` lê apenas as configurações de persistência MongoDB; `GorillazDiscordBot.Infra/Repository/MongoRepository.cs:22-27` instancia o `MongoClient`.
- `GorillazDiscordBot.Api/.env.example:1-12`, `README.md:69-84`, `Dockerfile` e `docker-compose.yml` não configuram Redis. O compose local inicia MongoDB e o bot.
- O Terraform ECS injeta apenas o segredo MongoDB para persistência (`Infra/AWS/ecs/main.tf:41-52`; `Infra/AWS/secrets/main.tf:17-38`).
- Há caches locais de processo: `ConcurrentDictionary` em `SettingsRepository` e `GuildInteractionRepository`, além de catálogo em memória em `ShopService`. Eles não são Redis nem cache distribuído.

## Hipóteses

- [ ] O erro Redis relatado pertence ao LuckyMonkey ou a outro sistema fora deste repositório.
- [ ] O erro ocorre em uma revisão/imagem diferente da analisada.
- [ ] Em mais de uma réplica do bot, os caches locais podem divergir; isso é um risco de consistência, não uma falha Redis.

## Linha do tempo

| Data e hora | Evento | Fonte |
| --- | --- | --- |
| 2026-09-18 | Busca estática em código, dependências, compose, ECS, Terraform e documentação. | Repositório local |

## Próximos passos

- [ ] Correlacionar o horário do erro com o nome do container e grupo de logs no CloudWatch.
- [ ] Confirmar a imagem e a revisão ECS que emitiram o erro.
- [ ] Obter o *stack trace* completo, removendo tokens, URLs privadas e outros segredos antes de registrá-lo.
- [ ] Investigar o repositório/configuração do LuckyMonkey somente se o log apontar para esse container.

## Resolução

Não há integração Redis a resolver neste repositório. A origem deve ser confirmada com evidências de runtime.

## Prevenção

Identificar sempre o serviço, container e revisão de implantação ao abrir incidentes de infraestrutura; isso evita investigar o repositório errado.
