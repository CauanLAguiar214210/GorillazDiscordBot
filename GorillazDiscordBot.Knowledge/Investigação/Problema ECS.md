---
tags:
  - investigacao
  - ecs
status: investigação estática concluída
atualizado: 2026-09-18
---

# Problema ECS

[[Home|Home]] · [[Investigação|← Investigação]] · [[Arquitetura/Infraestrutura|Infraestrutura]]

## Sintoma

Não foram fornecidos eventos, `stoppedReason` ou logs de produção. Esta nota registra condições do repositório que podem impedir o *deploy* ou a inicialização de uma tarefa ECS.

## Impacto

Se alguma das condições abaixo existir no ambiente AWS, a tarefa pode não iniciar ou o bot pode ficar em execução sem estar conectado ao Discord ou ao serviço de cassino.

## Evidências

- O workflow publica somente a imagem `gorillaz-discord-bot` em `.github/workflows/aws.yml:58-70`, mas `task-definition.template.json:40-42` exige a imagem essencial `luckymonkey.api:latest`.
- `Infra/AWS/ecr/main.tf:3-4` cria somente o repositório ECR do bot. `LUCKYMONKEY_INTEGRACAO.md:9-11` indica que o LuckyMonkey pertence a outro repositório.
- A definição de tarefa injeta os segredos LuckyMonkey nos dois containers (`task-definition.template.json:27-28,51-52`), enquanto `Infra/AWS/secrets/main.tf:3-38` e `Infra/AWS/ecs/iam.tf:50-73` declaram/autorizam apenas os segredos Discord, OWM e MongoDB.
- O sidecar grava no grupo `/ecs/gorillaz-discord-bot/luckymonkey` (`task-definition.template.json:57-63`), mas o Terraform cria somente `/ecs/${project}` (`Infra/AWS/ecs/main.tf:20-24`), sem `awslogs-create-group`.
- O Terraform define apenas o container `bot` (`Infra/AWS/ecs/main.tf:35-63`), enquanto o workflow registra a tarefa com `bot` e `luckymonkey` (`task-definition.template.json:9-65`). Isso cria deriva entre IaC e CD.
- Não há *health check* no Dockerfile, na definição de tarefa ou no serviço ECS; também não há *load balancer* configurado para o serviço (`Infra/AWS/ecs/main.tf:70-85`).
- A URL `LUCKY_MONKEY_URL=http://localhost:8080` é compatível apenas se o sidecar estiver presente. O `docker-compose.yml` local não sobe LuckyMonkey, reduzindo a paridade local.
- Se `DISCORD_TOKEN` estiver vazio, `DiscordBotService` registra erro crítico e retorna sem encerrar o host (`GorillazDiscordBot.Api/DiscordBotService.cs:95-100`). Sem *health check*, uma tarefa pode aparentar estar saudável sem o bot conectado.

## Hipóteses

- [ ] A tarefa falha ao iniciar porque a imagem LuckyMonkey não existe ou não está acessível no ECR configurado.
- [ ] A task falha ao resolver os segredos LuckyMonkey porque os ARNs ou permissões da *execution role* não existem fora do Terraform.
- [ ] A criação de logs falha porque o grupo do sidecar não existe.
- [ ] Uma aplicação posterior de Terraform remove o sidecar da definição criada pelo workflow.
- [ ] A tarefa está `RUNNING`, mas o bot está inoperante por token ausente ou inválido.

Nenhuma hipótese está confirmada sem eventos ECS, logs e a revisão de tarefa efetivamente implantada.

## Linha do tempo

| Data e hora | Evento | Fonte |
| --- | --- | --- |
| 2026-09-18 | Levantamento estático das configurações ECS, Terraform e workflow. | Repositório local |

## Próximos passos

- [ ] Consultar `stoppedReason`, eventos do serviço e a revisão ECS ativa, sem expor segredos.
- [ ] Confirmar a existência e o *digest* das imagens do bot e LuckyMonkey.
- [ ] Validar os grupos de logs, os ARNs dos segredos e as permissões efetivas da *execution role*.
- [ ] Comparar a definição de tarefa ativa com `task-definition.template.json` e `Infra/AWS/ecs/main.tf`.
- [ ] Verificar se `DISCORD_TOKEN` está presente na task sem registrar o valor.

## Resolução

Ainda não confirmada; faltam evidências de execução no ambiente AWS.

## Prevenção

Manter a definição de tarefa em uma única fonte de verdade, versionar/provisionar todos os recursos exigidos pelo sidecar e expor uma verificação de disponibilidade que falhe quando o bot não conseguir iniciar.
