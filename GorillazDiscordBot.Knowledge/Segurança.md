---
tags:
  - seguranca
atualizado: 2026-09-19
---

# Segurança

[[Home|Home]]

Boas práticas para o projeto, especialmente no trato de segredos e logs.

## Segredos

- Tokens/chaves nunca entram no repositório:
  - `DISCORD_TOKEN`, `OWM_API_KEY`, `LUCKY_MONKEY_API_KEY`, `CASINO_JWT_SIGNING_KEY` ficam em `.env` local (ignorado) ou Secrets Manager.
  - O template público é `GorillazDiscordBot.Api/.env.example` (valores vazios).
- No ECS/CI os segredos chegam por ARNs/vars do ambiente (`DISCORD_TOKEN_ARN`, `MONGODB_CONN_ARN`, `LUCKY_MONKEY_*_ARN`) — nem o workflow nem a task definition devem embutir valores.
- Não commitar `.env`, `*.pem`, chaves ou strings de conexão com credenciais. Conferir `git status`/`git diff` antes de commitar.

## Logs e investigação

- Ao investigar, registre presença/ausência de um segredo sem revelar o valor (ex.: "token presente e acessível").
- Remova `Authorization`, `X-Api-Key`, JWT, cookies e URLs privadas de qualquer *stack trace* antes de anexar.
- `DiscordBotService` registra erro crítico quando `DISCORD_TOKEN` está vazio — mas evite imprimir o próprio token.

## Superfície de exposição

- Validação de intents/escopos depende do **Portal Discord** (fora do repositório) — ver [[Integrações/Discord|Discord]] e [[Ideias/Validar intents e escopos no Portal Discord|Validar intents]].
- Segredos do sidecar LuckyMonkey ainda não são provisionados pelo Terraform — risco para a task iniciar — ver [[Investigação/Problema ECS|Problema ECS]].

## Checklist antes de abrir PR

- [ ] Nenhum segredo em arquivos, testes ou exemplos.
- [ ] `.gitignore`/`.dockerignore` cobrem `.env` e artefatos locais.
- [ ] Logs novos não capturam tokens/keys.
- [ ] Variáveis sensíveis adicionadas ao `.env.example` (e, em produção, ao gerenciador de segredos).