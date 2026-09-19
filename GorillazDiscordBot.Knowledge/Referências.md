---
tags:
  - referencias
atualizado: 2026-09-19
---

# Referências externas

[[Home|Home]]

Documentação oficial usada no projeto. URLs de documentação para programação.

## Stack principal

- [.NET 9](https://learn.microsoft.com/dotnet/) — runtime e bibliotecas (Generic Host, DI, BackgroundService).
- [C#](https://learn.microsoft.com/dotnet/csharp/) — guia de referência e tutoriais.
- [Discord.Net](https://docs.discordnet.dev/) — gateway, comandos prefixados, interações, intents.
- [MongoDB.Driver](https://www.mongodb.com/docs/drivers/csharp/) — driver C# de persistência.
- [Microsoft.Extensions.Hosting](https://learn.microsoft.com/dotnet/core/extensions/generic-host) — hospedagem e ciclo de vida.

## Qualidade e testes

- [xUnit](https://xunit.net/) · [FluentAssertions](https://fluentassertions.com/) · [NSubstitute](https://nsubstitute.github.io/) — suíte do `GorillazDiscordBot.Tests`.

## Entrega/ops

- [AWS ECS/Fargate](https://docs.aws.amazon.com/ecs/) — tasks, serviços e sidecars.
- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws/latest/docs) — ECR, ECS, secrets, networking.
- [GitHub Actions](https://docs.github.com/actions) — workflows `ci.yml` e `aws.yml`.
- [GitHub Packages / NuGet source](https://docs.github.com/packages) — fonte `github-luckymonkey` para dependências privadas.
- [Docker](https://docs.docker.com/) — imagem do bot e compose local.

## Infraestrutura observada

- CloudWatch (logging opcional via `AWS_LOG_GROUP`). Tenor (mídia) — ver [[Integrações/Tenor|Tenor]].