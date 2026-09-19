---
tags:
  - projetos
  - infra
atualizado: 2026-09-19
---

# Infra

[[Projetos|← Projetos]] · [[Projetos/Api|Api]] · [[Projetos/Domain|Domain]] · [[Projetos/Tests|Tests]]

Persistência MongoDB, serviços externos e infraestrutura AWS (Terraform).

## Estrutura

```text
GorillazDiscordBot.Infra/
├── Configuration/            # MongoMappings (BSON class maps), BotOptions, MongoOptions
├── Repository/               # MongoRepository (base genérica), SettingsRepository, e repositórios por entidade
├── Services/                 # MongoDBService, GifUrlService
└── AWS/                      # Terraform
    ├── ecr/                  # repositório da imagem do bot
    ├── ecs/                  # service/task + IAM
    ├── networking/           # VPC/rede
    └── secrets/              # Secrets Manager (Discord, OWM, MongoDB)
```

## Pontos-chave

- Coleções nomeadas por tipo (`typeof(T).Name`) — ver [[Coleções MongoDB|Coleções MongoDB]].
- `MongoMappings.Register` centraliza mapeamentos BSON/`ObjectId` e `IdGenerator`.
- `SettingsRepository<T>` faz cache concorrente por guild + upsert.
- Terraform: `GorillazDiscordBot.Infra/AWS/...` (obs.: o path correto é dentro do projeto Infra).

Estado atual do ECS/IaC e pendências: [[Investigação/Problema ECS|Problema ECS]].