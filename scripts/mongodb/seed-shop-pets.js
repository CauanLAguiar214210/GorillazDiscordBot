// seed-shop-pets.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria PET (ItemCategory 4) —
// pets passivos; cada cópia sobe o nível e o bônus permanente.
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-pets.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-pets.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-pets.js
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
const CATEGORY = 4;                 // ItemCategory.Pet
const BOOST_NONE = 0;               // BoostEffect.None
const UPGRADE = { None: 0, Daily: 1, Work: 2, Rob: 3, AssetIncome: 4, Savings: 5 };
const RELIC_NONE = 0;               // RelicEffect.None
const GAME_ALL = 0;                 // RelicGameType.All
const MAX_LEVEL = 10;

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "pet_macaco", name: "Macaco-Caçador",       emoji: "🐒", price: 15000, sort: 32, effect: UPGRADE.Daily, value: 2, desc: "+2% no daily por nível. Máximo de 2 tipos de pets." },
    { key: "pet_gorila", name: "Gorila-Guarda-Costas", emoji: "🦍", price: 40000, sort: 33, effect: UPGRADE.Work,  value: 3, desc: "+3% no trabalho por nível. Máximo de 2 tipos de pets." },
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
    MaxQuantity: MAX_LEVEL,
    IsActive: true,
    IsPlaceholder: false,
    SortOrder: i.sort,
    RelicEffect: RELIC_NONE,
    RelicGame: GAME_ALL,
    RelicValue: 0,
    UpgradeEffect: i.effect,
    UpgradeValue: i.value,
    VehicleType: 0,
    RequiredLicense: null
}));

// ---- Pré-checagem / deduplicação ---------------------------------------------
const shop = db.getCollection("ShopItem");

print(`Pets hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

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
    print("Nada a inserir — todos os pets já existem.");
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
print(`pets: ${shop.countDocuments({ Category: CATEGORY })}`);
print(EJSON.stringify(shop.findOne({ Key: "pet_gorila" }), null, 2));

quit(0);
