---
tags:
  - comandos
  - loja
atualizado: 2026-09-19
---

# Loja

[[Comandos|← Comandos]] · [[Comandos/Economia|Economia]]

Compra de itens, catálogo e inventário.

## Comandos

| Área | Módulo |
| --- | --- |
| Slash | `Commands/Shop/ShopSlashModule.cs` |
| Prefixado | `Commands/ShopModule.cs` |

## Detalhes

`ShopService` coordena catálogo, compras e inventário, incluindo saldo e concorrência por usuário. Itens em `Domain/Entity/Economy/ShopItem.cs`; persistência em `IShopRepository`. O catálogo pode ser semeado pelos scripts de `scripts/mongodb/` (ver [[Operação/Execução local|Execução local]]).