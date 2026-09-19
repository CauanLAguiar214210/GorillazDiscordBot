---
tags:
  - índice
  - home
atualizado: 2026-09-19
---

# Home

Base de conhecimento do **GorillazDiscordBot**: um bot Discord em .NET 9 com economia, cassino via LuckyMonkey, loja e moderação.

## Áreas

- [[Arquitetura|Arquitetura]] — visão geral, backend, banco de dados e infraestrutura.
- [[Comandos|Comandos]] — catálogo dos comandos e módulos do bot.
- [[Investigação|Investigação]] — problemas investigados, hipóteses e resoluções.
- [[Operação|Operação]] — execução local, AWS/ECS, logs e troubleshooting.
- [[Integrações|Integrações]] — LuckyMonkey, Discord, Tenor e AWS.
- [[Desenvolvimento|Desenvolvimento]] — guias para adicionar comandos, repositórios, serviços e testes.
- [[Decisões|Decisões]] — decisões de arquitetura (ADR).
- [[Agentes|Agentes]] — agentes de IA (OpenCode, Qwen, Claude) e como são usados no projeto.
- [[Glossário|Glossário]] — termos do domínio.
- [[Diário|Diário]] — notas diárias (daily notes).
- [[Reuniões|Reuniões]] — registro de reuniões, pauta e decisões.
- [[Ideias|Ideias]] — backlog e ideias com status.

## Referência

- [[Onboarding|Onboarding]] — ordem de leitura e setup para quem chega.
- [[Projetos|Projetos]] — mapa de código por projeto (Api, Domain, Infra, Tests).
- [[Coleções MongoDB|Coleções MongoDB]] — coleções, índices e shapes de dados.
- [[Git e entrega|Git e entrega]] — branches, CI/CD e deploy.
- [[Segurança|Segurança]] — segredos, logs e checklist de PR.
- [[Changelog|Changelog]] — histórico de versões.
- [[Referências|Referências]] — documentação externa útil.
- [[Dataview|Dataview]] — instalação e consultas para o painel.
- [[Canvas/Arquitetura.canvas|Arquitetura (canvas)]] · [[Canvas/Mapa do vault.canvas|Mapa do vault (canvas)]] — mapas visuais.

## Convenções

- Cada área tem um MOC (`Area.md`) que indexa suas notas.
- As barras de navegação usam caminho completo: `Pasta/Nota` (ex.: `[[Arquitetura/Backend|Backend]]`).
- Use `Templates/` para criar novas notas padronizadas.
- Mantenha `atualizado: YYYY-MM-DD` no frontmatter (usado pelo [[Dataview|Dataview]]).

## Painel (Dataview)

> Blocos ativam após instalar o plugin — veja [[Dataview|Dataview]].

````text
```dataview
TABLE status, atualizado
FROM #ideias
WHERE status
SORT status ASC
```
````

````text
```dataview
TABLE status, atualizado
FROM #investigacao
SORT atualizado DESC
```
````