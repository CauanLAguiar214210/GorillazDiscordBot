---
tags:
  - operacao
  - logs
atualizado: 2026-09-19
---

# Logs e observabilidade

[[Operação|← Operação]] · [[Operação/AWS|AWS/ECS]]

- Todo log passa pelo `ILogger` do .NET.
- Com `AWS_LOG_GROUP`/`AWS_REGION`, o provedor AWS envia ao CloudWatch.
- Falhas de criação de índices no boot são registradas como aviso (o host continua).
- Os `LogMessage` do Discord.Net são registrados em nível `Information`; transições `Connected`/`Disconnected`/`Resumed` não têm handlers dedicados hoje.

Ao investigar, identifique serviço, container e revisão — ver [[Investigação/Problema Redis|Problema Redis]] como exemplo de disciplina na investigação.