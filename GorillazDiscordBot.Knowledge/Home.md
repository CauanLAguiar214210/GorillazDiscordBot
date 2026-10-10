---
tags:
  - indice
  - home
tipo: moc
atualizado: 2026-10-10
---

# Home

Base de conhecimento do **GorillazDiscordBot**: um bot Discord em .NET 9 com economia, cassino via LuckyMonkey, loja e moderação.

## Áreas

- [[MOCs/Arquitetura|Arquitetura]] — visão geral, backend, banco de dados e infraestrutura.
- [[MOCs/Comandos|Comandos]] — catálogo dos comandos e módulos do bot.
- [[MOCs/Investigação|Investigação]] — problemas investigados, hipóteses e resoluções.
- [[MOCs/Operação|Operação]] — execução local, AWS/ECS, logs e troubleshooting.
- [[MOCs/Integrações|Integrações]] — LuckyMonkey, Discord, Tenor e AWS.
- [[MOCs/Desenvolvimento|Desenvolvimento]] — guias para adicionar comandos, repositórios, serviços e testes.
- [[MOCs/Projetos|Projetos]] — mapa de código por projeto (Api, Domain, Infra, Tests).
- [[MOCs/Decisões|Decisões]] — decisões de arquitetura (ADR).
- [[MOCs/Agentes|Agentes]] — agentes de IA (OpenCode, Qwen, Claude) e como são usados no projeto.
- [[MOCs/Glossário|Glossário]] — termos do domínio.
- [[MOCs/Diário|Diário]] — notas diárias (daily notes).
- [[MOCs/Reuniões|Reuniões]] — registro de reuniões, pauta e decisões.
- [[MOCs/Ideias|Ideias]] — backlog e ideias com status.
- [[MOCs/Vault|Vault]] — onboarding, guia, entrega, segurança, painéis e referências.

## Referência

- [[Arquitetura/System Design|System Design]] — design consolidado do sistema (contexto, camadas, dados, deploy).
- [[Arquitetura/Coleções MongoDB|Coleções MongoDB]] — coleções, índices e shapes de dados.
- [[Vault/Onboarding|Onboarding]] — ordem de leitura e setup para quem chega.
- [[Vault/Guia do vault|Guia do vault]] — padrões de estrutura, tags e frontmatter.
- [[Vault/Entrega|Entrega]] — branches, CI/CD e deploy.
- [[Vault/Segurança|Segurança]] — segredos, logs e checklist de PR.
- [[Vault/Painéis|Painéis]] — painéis nativos (Bases) do vault.
- [[Vault/Referências|Referências]] — documentação externa útil.
- [[Vault/Changelog|Changelog]] — histórico de versões.
- [[Canvas/Arquitetura.canvas|Arquitetura (canvas)]] · [[Canvas/Mapa do vault.canvas|Mapa do vault (canvas)]] — mapas visuais.

## Convenções

- A raiz contém apenas este índice; os MOCs de área ficam em `MOCs/` e toda nota vive numa pasta de área.
- Cada área tem um MOC em `MOCs/` que indexa suas notas; novas notas seguem `Templates/`.
- Barras de navegação usam caminho completo: `Pasta/Nota` (ex.: `[[Arquitetura/Backend|Backend]]`).
- Frontmatter mínimo: `tags`, `tipo`, `status` (quando aplicável) e `atualizado: YYYY-MM-DD`. Ver [[Vault/Guia do vault|Guia do vault]].

## Painéis

Os painéis usam o recurso nativo **Bases** (ver [[Vault/Painéis|Painéis]]).

![[Vault/Painéis.base]]
