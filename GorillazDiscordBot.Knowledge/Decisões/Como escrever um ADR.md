---
tags:
  - decisoes
  - adr
atualizado: 2026-09-19
---

# Como escrever um ADR

[[Decisões|← Decisões]]

Um ADR (Architecture Decision Record) registra **o quê**, **por quê** e **as consequências** de uma decisão estrutural.

## Seções

| Seção | Conteúdo |
| --- | --- |
| Título | `ADR-0001 - <Nome da decisão>`. |
| Status | `Proposto` · `Aceito` · `Superado`. |
| Contexto | O problema ou restrição que motivou a decisão. |
| Decisão | A escolha feita e a alternativa considerada. |
| Consequências | Positivas e negativas; o que fica mais fácil ou mais difícil. |
| Notas | Referências (código, [[Decisões|outras decisões]], links externos). |

## Regras

- Uma decisão por nota, curta e direta.
- Use o template `Templates/ADR.md`.
- Alterar uma decisão não apaga a nota: registra-se o estado `Superado` e cria-se uma nova ADR que aponta para a anterior.