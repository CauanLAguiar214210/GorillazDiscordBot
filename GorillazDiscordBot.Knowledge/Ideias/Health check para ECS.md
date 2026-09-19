---
tags:
  - ideias
  - ecs
status: ideia
atualizado: 2026-09-19
---

# Health check para ECS

[[Ideias|← Ideias]]

Expor uma verificação de disponibilidade no bot para que o serviço ECS falhe a task quando o bot não conseguir iniciar (ex.: token vazio). Hoje não há health check — uma task pode aparentar saudável sem o bot conectado.

**Motivação:** [[Investigação/Problema ECS|Problema ECS]] (diagnóstico estático). Ver [[Operação/AWS|AWS/ECS]].