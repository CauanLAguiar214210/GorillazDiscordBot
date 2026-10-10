---
tags:
  - vault
  - paineis
tipo: nota
atualizado: 2026-10-10
---

# Painéis

Índices automáticos do vault usando o recurso nativo **Bases**. A definição fica em `Vault/Painéis.base` e é embutida em [[Home|Home]] e [[Vault|Vault]].

[[Home|Home]] · [[Vault|← Vault]]

## Views disponíveis

| View | Filtro | Uso |
| --- | --- | --- |
| Ideias | `#ideias` | Backlog com `status` (ver [[Ideias/Como usar|Como usar]]). |
| Investigações | `#investigacao` | Casos abertos/concluídos. |
| ADRs | `#decisoes` | Decisões e seus status. |
| Notas recentes | `atualizado` nos últimos 14 dias | O que mudou por último. |
| MOCs | `#moc` | Todas as notas-folha. |

## Como usar

- Bases é um recurso **nativo** (não requer plugin da comunidade). Para editar, abra `Vault/Painéis.base` no Obsidian.
- As consultas dependem do campo `atualizado: YYYY-MM-DD` e das tags de área. Mantenha-os em dia (ver [[Vault/Guia do vault|Guia do vault]]).
- Para embutir uma view específica em outra nota: `![[Vault/Painéis.base#Ideias]]`.
