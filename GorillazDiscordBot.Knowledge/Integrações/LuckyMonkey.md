---
tags:
  - integracao
  - luckymonkey
atualizado: 2026-09-19
---

# LuckyMonkey

[[Integrações|← Integrações]] · [[Comandos/Cassino|Cassino]]

Microserviço de cassino consumido pelo bot por HTTP.

## Configuração

| Variável | Finalidade |
| --- | --- |
| `LUCKY_MONKEY_URL` | Base URL (ex.: `http://localhost:8080` para o sidecar). |
| `LUCKY_MONKEY_API_KEY` | Chave de API enviada nas requisições. |
| `CASINO_JWT_SIGNING_KEY` | Chave de assinatura para JWT do cassino. |

## Uso no código

- `CasinoApiClient` — cliente HTTP autenticado.
- `Commands/Casino/CasinoApiFlow.cs` — fluxo das chamadas.
- `PayoutService` — débitos/créditos de apostas e resultados.

## Implantação

No ECS, o LuckyMonkey é o sidecar `luckymonkey.api` na task (ver [[Operação/AWS|AWS/ECS]]); a integração pertence a outro repositório. Em local via `docker-compose`, o serviço **não** é levantado (paridade local reduzida).