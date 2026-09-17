// seed-shop-upgrades.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria UPGRADE (ItemCategory 6) —
// melhorias permanentes do manobrista (vagas e valor por carro).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-upgrades.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-upgrades.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-upgrades.js
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
const CATEGORY = 6;                 // ItemCategory.Upgrade
const BOOST_NONE = 0;               // BoostEffect.None
const UPGRADE = { None: 0, Daily: 1, Work: 2, Rob: 3, AssetIncome: 4, Savings: 5,
                  ManobristaBase: 6, ManobristaVagas: 7 };
const RELIC_NONE = 0;               // RelicEffect.None
const GAME_ALL = 0;                 // RelicGameType.All

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "upgrade_vagas_1", name: "Ampliação Simples",   emoji: "🅿️", price: 12000, sort: 46, max: 3, effect: UPGRADE.ManobristaVagas, value: 2,  desc: "+2 vagas no estacionamento particular por unidade." },
    { key: "upgrade_vagas_2", name: "Ampliação Premium",   emoji: "🅿️", price: 40000, sort: 47, max: 2, effect: UPGRADE.ManobristaVagas, value: 5,  desc: "+5 vagas no estacionamento particular por unidade." },
    { key: "upgrade_base_1",  name: "Manutenção Leve",     emoji: "🔧", price: 15000, sort: 48, max: 3, effect: UPGRADE.ManobristaBase,  value: 10, desc: "+10% no valor por carro estacionado por unidade." },
    { key: "upgrade_base_2",  name: "Centro de Logística", emoji: "🔧", price: 45000, sort: 49, max: 2, effect: UPGRADE.ManobristaBase,  value: 20, desc: "+20% no valor por carro estacionado por unidade." },
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
    MaxQuantity: i.max,
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

print(`Melhorias hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

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
    print("Nada a inserir — todas as melhorias já existem.");
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
print(`melhorias: ${shop.countDocuments({ Category: CATEGORY })}`);
print(EJSON.stringify(shop.findOne({ Key: "upgrade_base_2" }), null, 2));

quit(0);
