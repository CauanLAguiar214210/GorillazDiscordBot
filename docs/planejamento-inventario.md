# Planejamento — Sistema de Inventário, Equipamentos e Patrimônio

> Documento de planejamento em fases para a funcionalidade de inventário:
> roupas, veículos, imóveis, terrenos (com construção de negócios) e sistema de dívidas.

## 1. Conceito geral

O jogador pode **equipar 1 item por slot** simultaneamente:

| Slot | Categoria | Efeito | Custo diário |
|---|---|---|---|
| 👕 | Roupas (`Clothing`) | Visual + bônus passivo opcional (+%) | ❌ |
| 🚗 | Veículos (`Vehicle`) | Boost (+% daily/trabalho/etc.), só quando equipado | ✅ |
| 🏠 | Imóveis (`Property`) | Renda/dia quando equipado | ✅ |
| 🗺️ | Terrenos (`Land`) | Renda via **negócios** (todos os terrenos rendem); equipar é só visual | ✅ terreno + negócios |
| ⌚ | Relíquias (`Relic`) | Bônus no cassino (existente) | ✅ |
| 💍 | Joias (`Jewel`) | Visual + bônus opcional | ✅ |
| 🏪 | Negócios (`Business`) | Não entra no inventário; instalado em terrenos | ✅ por nível |

Regras globais:

- Itens com efeito (veículo, roupa) só aplicam bônus quando **equipados**. Pets continuam sempre ativos.
- Itens equipados (e terrenos/negócios) geram **custo de manutenção diário no banco**.
- Despesas não pagas viram **dívida com juros (~10%/dia)**.
- Dívida é quitada **automaticamente pela renda** (primeira prioridade).
- Com dívida aberta, o jogador não pode **comprar, construir, evoluir, equipar/desequipar** nem **abrir negócios** (vender continua liberado).
- Dívida tem **teto = patrimônio total**; ao atingir, itens são liquidados (os mais caros primeiro) para abater.
- Terrenos são **únicos por tipo** e abrigam **vários negócios** limitados pelo **espaço/tamanho** (`Size`).

## 2. Mecânica de terrenos e negócios

- **Terreno** = item de loja único por tipo (`MaxQuantity` 1), campo `Size` = espaço total de construção.
- **Negócio** = item de catálogo (categoria `Business`), instalado no terreno via comando (não vira item do inventário).
  - `SpaceCost`: espaço que o negócio ocupa.
  - Cada **nível evoluído ocupa +1 espaço** (ex.: nível 1 ocupa `SpaceCost`, nível 2 ocupa `SpaceCost + 1`...).
  - `IncomePerLevel`: renda/dia por nível.
  - `DailyCostPerLevel`: custo de manutenção/dia por nível.
  - `Price`: custo de instalação (nível 1); evoluir custa `Price × (novo nível)`.
- Bloqueio de construção quando o espaço usado total = `Size`.
- Renda coletada no `daily` (junto com assets), **todos os terrenos possuídos** rendem, equipado ou não.
- Vender terreno remove os negócios dele; reembolso 50%.

## 3. Manutenção diária + dívida (job da meia-noite)

Ordem no `EconomyMaintenanceService`:

1. Taxa bancária + juros da poupança (existente, inalterado).
2. **Juros da dívida** ≈ **+10%/dia** composto (`DebtInterest`).
3. **Manutenção**: cobra em cascata **banco → carteira → dívida**; o que faltar vira dívida (`Maintenance`); nunca saldo negativo.
4. **Teto = patrimônio**: se `Debt > PatrimônioTotal`, o bot **liquida itens** (mais caros primeiro) até `Debt ≤ Patrimônio` (`Seizure`).

## 4. Pagamento automático da dívida

Toda renda (daily, trabalho, negócios, ativos, cassino) passa por `TryReceiveIncomeAsync`:

1. Quita a dívida primeiro (até `min(renda, dívida)`) → transação `DebtPayment`.
2. O restante vai para a carteira → transação normal (`Income`/`Daily`/etc.).

## 5. Modelo de dados

### `ShopItem` (`Domain/Entity/Economy/ShopItem.cs`)
- `ItemCategory` += `Clothing, Vehicle, Property, Land, Jewel, Business`.
- Novo enum `EquipSlot`: `None, Relic, Clothing, Vehicle, Property, Land, Jewel`.
- Novo enum `ItemType` (subtipo):
  - Imóvel: `Casa, Apartamento, Mansao, Cobertura`
  - Veículo: `Carro, Moto, Jato, Iate`
  - Terreno: `Urbano, Industrial, BeiraMar, Rural`
  - Joia: `Anel, Colar, Coroa`
  - Negócio: `Restaurante, Loja, Hotel` etc. (definido no catálogo)
- Campos novos: `EquipSlot Slot`, `ItemType Tipo`, `ulong DailyCost`.
- Campos de terreno/negócio: `int Size`, `int SpaceCost`, `ulong IncomePerLevel`, `ulong DailyCostPerLevel`.

### `UserBusiness` (nova entidade/coleção)
```
Id, UserId, LandKey, BusinessKey, Level, CreatedAt, LastCollectedAt
```
Índice único `(UserId, LandKey, BusinessKey)`. Um registro por negócio instalado em um terreno.

### `InventoryItem`
Sem mudança estrutural (`IsEquipped` reaproveitado por slot).

### `EconomyProfile`
- `ulong Debt`
- `DateTime? LastDebtInterestDate`

### `EconomyTransactionType` += `Maintenance, Construction, Debt, DebtInterest, DebtPayment, Seizure`

### `MongoMappings`
Mapear campos novos do `ShopItem`, `EconomyProfile` (+`Debt`) e `UserBusiness`.

## 6. Fases de implementação

### Fase 1 — Domínio e modelos
- Atualizar `ShopItem` (enums + campos).
- Criar `UserBusiness`.
- Atualizar `EconomyProfile` (dívida) e `EconomyTransactionType`.
- Atualizar `MongoMappings`.

### Fase 2 — Repositórios
- `ShopRepository`/`IShopRepository`: `GetEquippedAsync()` (bulk p/ job e por usuário p/ perfil).
- `UserBusinessRepository` novo (CRUD + índice único; `GetAllAsync` p/ job).
- `IEconomyRepository`/`EconomyRepository`:
  - `TryReceiveIncomeAsync` (paga dívida primeiro).
  - `TryDeductMaintenanceAsync` (banco → carteira → dívida, nunca negativo).
  - `ApplyDebtInterestAsync` (juros 10%/dia).
  - `GetProfileAsync` / `GetDebtAsync`.

### Fase 3 — Equip multi-slot
- `ShopService.EquipAsync`: valida slot, desequipa **somente o mesmo slot**, equipa o novo (mantém lock por usuário).
- `UnequipAsync` por slot.
- `GetEquippedRelicAsync`: filtrar por `Category == Relic` (não pegar roupa equipada).
- `GetUpgradePercentCoreAsync`: incluir **itens equipados** de Veículo/Roupa (bônus ativos só quando equipados).
- `GetEquippedItemsAsync(userId)`: inventário equipado + catálogo (para `/perfil`).

### Fase 4 — Terrenos e negócios
- `ShopService.ApplyAssetIncomesAsync`:
  - Imóvel: renda **somente quando equipado**.
  - Negócios de todos os terrenos: renda `IncomePerLevel × nível`, coletada no daily.
- `LandBusinessService` (novo):
  - `BuildAsync` (`/construir`): instala nível 1, valida espaço `≤ Size`.
  - `UpgradeAsync` (`/evoluir`): custo `Price × novo nível`, valida espaço com o novo nível.
  - `GetLandBusinessesAsync` (listagem para `/terrenos`).
  - Bloqueado com dívida aberta.
- Vender terreno limpa negócios vinculados.

### Fase 5 — Manutenção e dívida
- `EconomyMaintenanceService`: orquestrar os 4 passos da seção 3.
- `ShopService.GetPatrimonyAsync`: carteira + banco + poupança + preço de compra dos itens + terrenos + investido em negócios.
- `ShopService.LiquidateToCoverDebtAsync`: liquida os itens mais caros (e negócios/terrenos) até `Debt ≤ Patrimônio` (`Seizure`).
- Guardas de dívida em `BuyCoreAsync`, `EquipAsync`, `UnequipAsync`, `BuildAsync`, `UpgradeAsync`.

### Fase 6 — Organização patrimônio de despesas
- `/perfil`: carteira, banco, poupança, patrimônio, dívida, lucro/prejuízo por dia + **itens equipados por slot**.
- `/patrimonio`:
  - **Ativo**: soma completa do patrimônio (incluindo valor de itens/terrenos/negócios).
  - **Fluxo diário**: receitas/dia (ativos equipados + negócios) vs despesas/dia (manutenção equipados + terrenos/negócios + taxa bancária esperada + juros previstos) → lucro/prejuízo líquido.
  - **Dívida**: valor atual, juros acumulados, status de bloqueio.
- `/divida`: consulta da dívida atual.
- (Opcional) `/pagardivida <valor>`: quitar com banco manualmente.

### Fase 7 — Loja, inventário e catálogo
- `/loja` (slash): abas novas em `ShopCategoryChoice` — 👕 Roupas, 🚗 Veículos, 🏠 Imóveis, 🗺️ Terrenos, 💍 Joias, 🏪 Negócios.
- `GetCategoryMeta` / `GetCategoryId` / `ParseCategoryId` / `Describe*`: renda/dia, custo/dia, tipo e boost nos embeds.
- `/inventario` + `/equipar`/`/desequipar`: botões Equipar/Desequipar por slot, badges de custo/renda.
- `ShopModule` (prefixo): paridade dos comandos.
- `ShopService.DefaultCatalog()`: semear catálogo inicial + placeholders.
- Espelhar em `seed-shop-catalog.js`.

### Fase 8 — Comandos de construção (slash + prefixo)
- `/construir <terreno> <negócio>`.
- `/evoluir <terreno> <negócio>`.
- `/terrenos` (listagem com espaço usado/livre, negócios, níveis, renda/custo diário).

### Fase 9 — Testes
- Atualizar `ShopServiceTests`:
  - `EquipAsync` slot-aware (não usa mais `UnequipAllAsync` global).
  - `EquipAsync_ItemNaoRelic_Falha` → comportamento de "não equipável".
- Novos testes:
  - Equip multi-slot simultâneo e troca dentro do mesmo slot.
  - Limite de espaço/construção (vários negócios, níveis).
  - Custo de evolução crescente.
  - Renda de imóvel só equipado; renda de negócios em todos os terrenos.
  - Manutenção: banco → carteira → dívida (fallback).
  - Juros de 10%/dia na dívida.
  - Pagamento automático da dívida pela renda.
  - Teto de dívida + liquidação de itens.
  - Bloqueio de comprar/construir/equipar com dívida.
  - Dados de `/perfil` e `/patrimonio`.
  - `UserBusinessRepository` (índice, CRUD, migração na unificação de contas).

## 7. Decisões calibravéis (ajustáveis depois)

| Decisão | Valor padrão |
|---|---|
| Custo de instalação do negócio | `Price` |
| Custo de evolução | `Price × (novo nível)` |
| Juros da dívida | 10%/dia (composto) |
| Ordem de liquidação | itens mais caros primeiro |
| Lucro líquido | renda − manutenção |
| Reembolso de venda | 50% |

## 8. Notas
- Contas vinculadas (alts): a migração de inventário da `MigrateInventoryAsync` deve tratar terreno/negócios para o slot Land (mover `UserBusiness` na unificação).
- A dívida é por conta **principal** (após resolução de `MainUserId`), como todo o resto da economia.