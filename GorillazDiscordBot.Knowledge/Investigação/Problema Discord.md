---
tags:
  - investigacao
  - discord
status: investigação estática concluída
atualizado: 2026-09-18
---

# Problema Discord

[[Home|Home]] · [[Investigação|← Investigação]] · [[Arquitetura/Backend|Backend]] · [[Arquitetura/Infraestrutura|Infraestrutura]]

## Sintoma

Não há log de conexão, falha de comando ou erro de gateway anexado. Esta análise aponta condições do código e da configuração que podem explicar falhas de conexão, mensagens ou slash commands.

## Impacto

Configuração inválida de token, intents, permissões ou registro de comandos pode impedir que o bot conecte, leia comandos prefixados, processe eventos de membros/voz ou exponha slash commands.

## Evidências

- `DiscordSocketClient` é criado com `Guilds`, `GuildMessages`, `MessageContent`, `DirectMessages`, `GuildMembers` e `GuildVoiceStates` em `GorillazDiscordBot.Api/Program.cs:38-51`.
- O token depende exclusivamente de `DISCORD_TOKEN` (`Program.cs:18-29`). Quando vazio, o serviço registra erro crítico e retorna sem encerrar o host (`DiscordBotService.cs:95-103`).
- Login, início do cliente e carregamento de módulos não têm tratamento de exceção local (`DiscordBotService.cs:78-103`).
- O serviço registra handlers para interações, mensagens, membros, voz e guild (`DiscordBotService.cs:64-76`), mas não possui handlers explícitos de `Connected`, `Disconnected`, `Resumed` ou métricas de latência.
- Todos os `LogMessage` do Discord.Net são registrados como `Information` (`DiscordBotService.cs:128-131`), reduzindo a distinção visual de severidade no destino de logs.
- Slash commands são registrados após `Ready`; sem `DISCORD_DEV_GUILD_ID`, o registro é global e a própria aplicação informa que a propagação pode levar até uma hora (`DiscordBotService.cs:180-204`). A variável não aparece no template ECS.
- `WheelSlashModule` está marcado com `[Disabled]` e é ignorado pelo bootstrap (`Commands/Casino/WheelSlashModule.cs:11`; `DiscordBotService.cs:84-93`). O comando `/roda` não deve aparecer.
- O README orienta criar `GorillazDiscordBot.Api/.env`, mas `.dockerignore:3` o exclui da imagem; `docker-compose.yml:22` usa somente `DISCORD_TOKEN` do ambiente ou `.env` da raiz.
- O template ECS injeta `DISCORD_TOKEN` por Secrets Manager, mas também requer segredos LuckyMonkey não autorizados pelo Terraform. Essa falha pode impedir a task de iniciar antes da conexão Discord.

## Hipóteses

- [ ] `MessageContent` ou `GuildMembers` não está habilitado no Portal Discord, impedindo eventos ou comandos prefixados esperados.
- [ ] O token ECS está ausente, inválido, revogado ou aponta para outro ambiente.
- [ ] O bot foi convidado sem os escopos `bot` e `applications.commands`, ou sem permissões no canal.
- [ ] O registro global ainda está em propagação ou falhou sem nova tentativa após `Ready`.
- [ ] A task não alcança a conexão Discord ou não inicia devido a falha anterior de infraestrutura.

As hipóteses dependem do ambiente Discord/AWS e não são confirmadas pelo repositório.

## Linha do tempo

| Data e hora | Evento | Fonte |
| --- | --- | --- |
| 2026-09-18 | Levantamento estático da integração Discord e da configuração de execução. | Repositório local |

## Próximos passos

- [ ] Consultar logs e eventos da revisão ECS ativa, sem imprimir o token.
- [ ] Confirmar valor presente e acesso ao secret `DISCORD_TOKEN`, sem revelar o valor.
- [ ] Validar no Portal Discord os intents privilegiados, os escopos do convite e as permissões da guild/canal de teste.
- [ ] Definir `DISCORD_DEV_GUILD_ID` em ambiente de desenvolvimento e testar o registro de comandos na guild.
- [ ] Conferir se o comando esperado está desabilitado ou se seu módulo foi carregado.

## Resolução

Ainda não confirmada; são necessários logs do gateway e dados da revisão ECS em execução.

## Prevenção

Registrar transições de conexão e desconexão com severidade adequada, validar a presença das configurações obrigatórias no boot e manter um teste de slash commands em uma guild de desenvolvimento.
