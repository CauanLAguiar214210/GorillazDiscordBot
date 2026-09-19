---
tags:
  - operacao
  - troubleshooting
atualizado: 2026-09-19
---

# Troubleshooting

[[Operação|← Operação]] · [[Operação/Execução local|Execução local]] · [[Operação/AWS|AWS/ECS]]

Problemas comuns e por onde começar a investigar.

| Sintoma | Caminho de investigação |
| --- | --- |
| Bot não conecta / não responde comandos | Ver [[Investigação/Problema Discord|Problema Discord]] (token, intents, escopos, registro). |
| Task ECS não inicia ou reinicia | Ver [[Investigação/Problema ECS|Problema ECS]] (imagens, segredos, grupos de log, health check). |
| Erro Redis | Provavelmente pertence a outro serviço (ex.: LuckyMonkey); ver [[Investigação/Problema Redis|Problema Redis]]. |
| Slash command demora a aparecer | Registro global em propagação; usar `DISCORD_DEV_GUILD_ID`. |
| Comando não aparece de jeito nenhum | Checar `[Disabled]` no módulo e o catálogo (`Api/Utils/CommandCatalog.cs`). |
| Cassino não responde | Verificar LuckyMonkey acessível/autenticado — [[Integrações/LuckyMonkey|LuckyMonkey]]. |

Regra de ouro: registre sempre serviço, container e revisão ao abrir um incidente.