// Seed inicial das Classificações (RankingTier) — ajuste os valores como quiser.
// Collection: RankingTier
// Como rodar: mongosh "mongodb://localhost:27017/gorillazbot" --file seed-ranking.js

use gorillazbot;

db.RankingTier.deleteMany({});

db.RankingTier.insertMany([
  { Key: "filhote",   Title: "Macaco Filhote",   Emoji: "🐵",      MinNetWorth: NumberLong(0),             SortOrder: 1,  IsActive: true },
  { Key: "bronze",    Title: "Macaco Bronze",    Emoji: "🥉",      MinNetWorth: NumberLong(10000),         SortOrder: 2,  IsActive: true },
  { Key: "prata",     Title: "Macaco Prata",     Emoji: "🥈",      MinNetWorth: NumberLong(100000),        SortOrder: 3,  IsActive: true },
  { Key: "ouro",      Title: "Macaco Ouro",      Emoji: "🥇",      MinNetWorth: NumberLong(1000000),       SortOrder: 4,  IsActive: true },
  { Key: "diamante",  Title: "Macaco Diamante",  Emoji: "💎",      MinNetWorth: NumberLong(100000000),     SortOrder: 5,  IsActive: true },
  { Key: "lendario",  Title: "Macaco Lendário",  Emoji: "🌟",      MinNetWorth: NumberLong(1000000000),    SortOrder: 6,  IsActive: true },
  { Key: "gorila_rei",Title: "Gorila Rei",       Emoji: "👑",      MinNetWorth: NumberLong(10000000000),   SortOrder: 7,  IsActive: true },
]);

// Exemplo de Hall of Fame — substitua pela lista que você vai inserir.
// Collection: HallOfFame
// Campos: UserId (id do Discord), Phase (ex: "Fase 1"), Title, Phrase, SortOrder
//
// db.HallOfFame.deleteMany({});
//
// db.HallOfFame.insertMany([
//   { UserId: NumberLong("123456789012345678"), Phase: "Fase 1", Title: "Rei da Selva", Phrase: "Primeiro a chegar, primeiro a dominar.", SortOrder: 1 },
// ]);

print("Seeding concluído!");