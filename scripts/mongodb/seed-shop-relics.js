// seed-shop-relics.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria RELIC (ItemCategory 3) —
// relógios equipáveis de cassino.
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-relics.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-relics.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-relics.js
//
// Idempotente: insertMany com filtro de Keys já existentes.
// ============================================================================

const defaultDbName = "gorillazbot";

const extractDatabaseName = (uri) => {
    const rest = String(uri).replace(/^mongodb(\+srv)?:\/\//i, "");
    const slash = rest.indexOf("/");
    if (slash === -1) return defaultDbName;
    const path = rest.substring(slash + 1);
    const q = path.indexOf("?");
    return (q === -1 ? path : path.substring(0, q)).trim() || defaultDbName;
};

const connectionString =
    process.env.MONGODB_CONNECTION_STRING ||
    process.argv.find((arg) => /^mongodb(\+srv)?:\/\//i.test(arg)) ||
    null;

if (connectionString) {
    db = connect(connectionString).getDB(extractDatabaseName(connectionString));
    print(`Conectado (db: ${extractDatabaseName(connectionString)})`);
}

// ---- Enums (int) — espelham ShopItem.cs ---------------------------------------
const CATEGORY = 3;                 // ItemCategory.Relic
const BOOST_NONE = 0;               // BoostEffect.None
const UPGRADE_NONE = 0;             // UpgradeEffect.None
const RELIC = { None: 0, GainBonus: 1, Cashback: 2 };
const GAME = { All: 0, Roulette: 1, Slots: 2, Blackjack: 3, Dice: 4, Coin: 5,
               Aviao: 6, VideoPoker: 7, Mines: 8, Limbo: 9, Rps: 10, Race: 11,
               Plinko: 12, Wheel: 13, HighLow: 14, Baccarat: 15 };

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "relogio",           name: "Relógio do Cassino",         emoji: "⌚", price: 100000, sort: 16, effect: RELIC.GainBonus, game: GAME.All,        value: 10, desc: "+10% em TODOS os ganhos de cassino." },
    { key: "relogio_slot",      name: "Relógio da Sorte",           emoji: "🎰", price: 120000, sort: 17, effect: RELIC.GainBonus, game: GAME.Slots,      value: 25, desc: "+25% nos ganhos da caça-níquel." },
    { key: "relogio_roleta",    name: "Relógio Vermelho",           emoji: "🔴", price: 150000, sort: 18, effect: RELIC.GainBonus, game: GAME.Roulette,   value: 20, desc: "+20% nos ganhos da roleta." },
    { key: "relogio_cash",      name: "Relógio do Reembolso",       emoji: "💸", price: 180000, sort: 19, effect: RELIC.Cashback,  game: GAME.All,        value: 15, desc: "Devolve 15% da aposta quando você perde." },
    { key: "relogio_dados",     name: "Relógio da Sorte dos Dados", emoji: "🎲", price: 90000,  sort: 20, effect: RELIC.GainBonus, game: GAME.Dice,       value: 20, desc: "+20% nos ganhos dos dados." },
    { key: "relogio_cara",      name: "Relógio das Duas Faces",     emoji: "🪙", price: 70000,  sort: 21, effect: RELIC.GainBonus, game: GAME.Coin,       value: 20, desc: "+20% nos ganhos de cara ou coroa." },
    { key: "relogio_aviao",     name: "Relógio da Decolagem",       emoji: "✈️", price: 110000, sort: 22, effect: RELIC.GainBonus, game: GAME.Aviao,      value: 20, desc: "+20% nos ganhos do aviaozinho." },
    { key: "relogio_poker",     name: "Relógio do Ás",              emoji: "🃏", price: 130000, sort: 23, effect: RELIC.GainBonus, game: GAME.VideoPoker, value: 20, desc: "+20% nos ganhos do poker de máquina." },
    { key: "relogio_minas",     name: "Relógio do Mineiro",         emoji: "⛏️", price: 100000, sort: 24, effect: RELIC.GainBonus, game: GAME.Mines,      value: 20, desc: "+20% nos ganhos das minas." },
    { key: "relogio_limbo",     name: "Relógio da Sorte",           emoji: "🔮", price: 90000,  sort: 25, effect: RELIC.GainBonus, game: GAME.Limbo,      value: 20, desc: "+20% nos ganhos do limbo." },
    { key: "relogio_jokenpo",   name: "Relógio do Desafiante",      emoji: "🤚", price: 75000,  sort: 26, effect: RELIC.GainBonus, game: GAME.Rps,        value: 20, desc: "+20% nos ganhos do jokenpô." },
    { key: "relogio_corrida",   name: "Relógio do Vencedor",        emoji: "🏆", price: 115000, sort: 27, effect: RELIC.GainBonus, game: GAME.Race,       value: 20, desc: "+20% nos ganhos da corrida." },
    { key: "relogio_plinko",    name: "Relógio da Gravidade",       emoji: "🎱", price: 105000, sort: 28, effect: RELIC.GainBonus, game: GAME.Plinko,     value: 20, desc: "+20% nos ganhos do plinko." },
    { key: "relogio_roda",      name: "Relógio da Fortuna",         emoji: "🎡", price: 90000,  sort: 29, effect: RELIC.GainBonus, game: GAME.Wheel,      value: 20, desc: "+20% nos ganhos da roda da fortuna." },
    { key: "relogio_altobaixo", name: "Relógio das Cartas",         emoji: "🃏", price: 85000,  sort: 30, effect: RELIC.GainBonus, game: GAME.HighLow,    value: 20, desc: "+20% nos ganhos do maior/menor." },
    { key: "relogio_bacara",    name: "Relógio do Croupier",        emoji: "🎴", price: 140000, sort: 31, effect: RELIC.GainBonus, game: GAME.Baccarat,   value: 20, desc: "+20% nos ganhos do baccarat." },
];

// ---- Monta os documentos -----------------------------------------------------
const docs = items.map(i => ({
    Key: i.key,
    Name: i.name,
    Emoji: i.emoji,
    Description: i.desc,
    Price: NumberLong(i.price),
    Category: CATEGORY,
    Effect: BOOST_NONE,
    DurationHours: 0,
    DailyIncome: NumberLong(0),
    MaxQuantity: 1,
    IsActive: true,
    IsPlaceholder: false,
    SortOrder: i.sort,
    RelicEffect: i.effect,
    RelicGame: i.game,
    RelicValue: i.value,
    UpgradeEffect: UPGRADE_NONE,
    UpgradeValue: 0,
    VehicleType: 0,
    RequiredLicense: null
}));

// ---- Pré-checagem / deduplicação ---------------------------------------------
const shop = db.getCollection("ShopItem");

print(`Relógios hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

const existingKeys = new Set(
    shop.find({ Key: { $in: docs.map(d => d.Key) } }, { Key: 1 }).toArray().map(d => d.Key)
);
const toInsert = docs.filter(d => !existingKeys.has(d.Key));
print(`Catálogo: ${docs.length} · já existem (pulados): ${existingKeys.size} · a inserir: ${toInsert.length}`);

// ---- insertMany --------------------------------------------------------------
if (toInsert.length > 0) {
    const result = shop.insertMany(toInsert, { ordered: false });
    print(`insertMany: inserted=${result.insertedIds ? Object.keys(result.insertedIds).length : toInsert.length}`);
} else {
    print("Nada a inserir — todos os relógios já existem.");
}

// ---- Índice único (opcional) -------------------------------------------------
try {
    shop.createIndex({ Key: 1 }, { unique: true, name: "uq_ShopItem_Key" });
    print("Índice único uq_ShopItem_Key garantido.");
}
catch (e) {
    print(`! Falha ao criar índice único (há Key duplicada?): ${e.message}`);
}

// ---- Validação ---------------------------------------------------------------
print("== RESULTADO ========================================================================");
print(`relógios: ${shop.countDocuments({ Category: CATEGORY })}`);
print(EJSON.stringify(shop.findOne({ Key: "relogio_bacara" }), null, 2));

quit(0);
