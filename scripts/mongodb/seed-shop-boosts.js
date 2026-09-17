// seed-shop-boosts.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria BOOST (ItemCategory 1).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-boosts.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-boosts.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-boosts.js
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
const CATEGORY = 1;                 // ItemCategory.Boost
const BOOST = { None: 0, DailyX2: 1, WorkX2: 2, RobShield: 3 };
const UPGRADE_NONE = 0;             // UpgradeEffect.None
const RELIC_NONE = 0;               // RelicEffect.None
const GAME_ALL = 0;                 // RelicGameType.All

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "dailyx2", name: "Luvas de Ouro",     emoji: "⚡", price: 3000, sort: 6, effect: BOOST.DailyX2,   duration: 0,  desc: "Seu próximo daily rende o DOBRO." },
    { key: "workx2",  name: "Capacete Turbo",    emoji: "💼", price: 4000, sort: 7, effect: BOOST.WorkX2,    duration: 0,  desc: "Seu próximo trabalho rende o DOBRO." },
    { key: "escudo",  name: "Escudo Anti-Roubo", emoji: "🛡️", price: 2500, sort: 8, effect: BOOST.RobShield, duration: 24, desc: "Fica imune a roubos por 24h." },
];

// ---- Monta os documentos -----------------------------------------------------
const docs = items.map(i => ({
    Key: i.key,
    Name: i.name,
    Emoji: i.emoji,
    Description: i.desc,
    Price: NumberLong(i.price),
    Category: CATEGORY,
    Effect: i.effect,
    DurationHours: i.duration,
    DailyIncome: NumberLong(0),
    MaxQuantity: 0,
    IsActive: true,
    IsPlaceholder: false,
    SortOrder: i.sort,
    RelicEffect: RELIC_NONE,
    RelicGame: GAME_ALL,
    RelicValue: 0,
    UpgradeEffect: UPGRADE_NONE,
    UpgradeValue: 0,
    VehicleType: 0,
    RequiredLicense: null
}));

// ---- Pré-checagem / deduplicação ---------------------------------------------
const shop = db.getCollection("ShopItem");

print(`Boosts hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

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
    print("Nada a inserir — todos os boosts já existem.");
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
print(`boosts: ${shop.countDocuments({ Category: CATEGORY })}`);
print(EJSON.stringify(shop.findOne({ Key: "escudo" }), null, 2));

quit(0);
