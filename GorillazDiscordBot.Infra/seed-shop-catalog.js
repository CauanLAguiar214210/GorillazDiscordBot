// ============================================================
// seed-shop-catalog.js — mongosh
// Popula a coleção ShopItem com o catálogo padrão.
// Usa $setOnInsert para NÃO sobrescrever itens já editados.
//
// Uso:
//   mongosh "mongodb://localhost:27017/gorillazbot" seed-shop-catalog.js
//   mongosh "$MONGODB_CONNECTION_STRING"            seed-shop-catalog.js
// ============================================================

var db = db.getSiblingDB("gorillazbot");

var items = [
  // ── Cosméticos ──────────────────────────────────────────
  { Key:"banana",  Name:"Banana Dourada",     Emoji:"🍌", Description:"Item de coleção do Gorillaz.",
    Price:1000,  Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:1 },
  { Key:"bone",    Name:"Boné do Gorillaz",   Emoji:"🧢", Description:"Um boné exclusivo de coleção.",
    Price:2500,  Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:2 },
  { Key:"trofeu",  Name:"Troféu Prime",       Emoji:"🏆", Description:"Mostre que você é o macaco alfa.",
    Price:5000,  Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:3 },
  { Key:"camisa",  Name:"Camisa 2D",          Emoji:"🎤", Description:"A camiseta oficial do vocalista.",
    Price:7500,  Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:4 },
  { Key:"mascara", Name:"Máscara de Murdoc",  Emoji:"🎭", Description:"A máscara do baixista, peça rara.",
    Price:12000, Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:5 },

  // ── Boosts ──────────────────────────────────────────────
  { Key:"dailyx2", Name:"Luvas de Ouro",      Emoji:"⚡", Description:"Seu próximo daily rende o DOBRO.",
    Price:3000,  Category:1, Effect:1, DurationHours:0,  DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:6 },
  { Key:"workx2",  Name:"Capacete Turbo",     Emoji:"💼", Description:"Seu próximo trabalho rende o DOBRO.",
    Price:4000,  Category:1, Effect:2, DurationHours:0,  DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:7 },
  { Key:"escudo",  Name:"Escudo Anti-Roubo",  Emoji:"🛡️", Description:"Fica imune a roubos por 24h.",
    Price:2500,  Category:1, Effect:3, DurationHours:24, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:false, SortOrder:8 },

  // ── Ativos (renda passiva) ──────────────────────────────
  { Key:"acoes",   Name:"Ações da Fazenda",   Emoji:"📈", Description:"Ações que rendem dividendos diários.",
    Price:20000,  Category:2, Effect:0, DurationHours:0, DailyIncome:2000,  MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:9 },
  { Key:"fazenda", Name:"Fazenda Gorillaz",   Emoji:"🚜", Description:"Produz bananas e rende moedas todo dia.",
    Price:60000,  Category:2, Effect:0, DurationHours:0, DailyIncome:6000,  MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:10 },
  { Key:"terreno", Name:"Terreno da Ilha",    Emoji:"🏞️", Description:"Alugado por turistas ricos. Rende diariamente.",
    Price:150000, Category:2, Effect:0, DurationHours:0, DailyIncome:15000, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:11 },
  { Key:"empresa", Name:"Empresa do Murdoc",  Emoji:"🏢", Description:"A maior corporação da Ilha. Lucro diário alto.",
    Price:500000, Category:2, Effect:0, DurationHours:0, DailyIncome:50000, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:12 },

  // ── Placeholders (Em breve) ─────────────────────────────
  { Key:"segredo", Name:"??? Ídolo Secreto",   Emoji:"🗿", Description:"Algo lendário está para chegar...",
    Price:0, Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:true, SortOrder:13 },
  { Key:"bau",     Name:"??? Baú do Oceano",   Emoji:"🗝️", Description:"Abaixo das ondas da Ilha...",
    Price:0, Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:true, SortOrder:14 },
  { Key:"evento",  Name:"??? Item de Evento",   Emoji:"🎆", Description:"Reservado para um evento especial...",
    Price:0, Category:0, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:0,
    IsActive:true, IsPlaceholder:true, SortOrder:15 },

  // ── Relógios equipáveis (bônus permanente no cassino) ──
  { Key:"relogio",      Name:"Relógio do Cassino",  Emoji:"⌚", Description:"+10% em TODOS os ganhos de cassino.",
    Price:100000, Category:3, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:16,
    RelicEffect:1, RelicGame:0, RelicValue:10 },
  { Key:"relogio_slot", Name:"Relógio da Sorte",    Emoji:"🎰", Description:"+25% nos ganhos da caça-níquel.",
    Price:120000, Category:3, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:17,
    RelicEffect:1, RelicGame:2, RelicValue:25 },
  { Key:"relogio_roleta", Name:"Relógio Vermelho",  Emoji:"🔴", Description:"+20% nos ganhos da roleta.",
    Price:150000, Category:3, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:18,
    RelicEffect:1, RelicGame:1, RelicValue:20 },
  { Key:"relogio_cash",   Name:"Relógio do Reembolso", Emoji:"💸", Description:"Devolve 15% da aposta quando você perde.",
    Price:180000, Category:3, Effect:0, DurationHours:0, DailyIncome:0, MaxQuantity:1,
    IsActive:true, IsPlaceholder:false, SortOrder:19,
    RelicEffect:2, RelicGame:0, RelicValue:15 },
];

var inserted = 0;
items.forEach(function (item) {
  var result = db.ShopItem.updateOne(
    { Key: item.Key },
    { $setOnInsert: item },
    { upsert: true }
  );
  if (result.upsertedCount > 0) inserted++;
});

print("Seed concluído. " + inserted + " item(ns) inserido(s) de " + items.length + ".");
