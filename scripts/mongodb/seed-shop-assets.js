// seed-shop-assets.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria ASSET (ItemCategory 2) —
// ativos de renda passiva.
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-assets.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-assets.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-assets.js
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
const CATEGORY = 2;                 // ItemCategory.Asset
const BOOST_NONE = 0;               // BoostEffect.None
const UPGRADE_NONE = 0;             // UpgradeEffect.None
const RELIC_NONE = 0;               // RelicEffect.None
const GAME_ALL = 0;                 // RelicGameType.All

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "acoes",   name: "Ações da Fazenda", emoji: "📈", price: 20000,  income: 2000,  sort: 9,  desc: "Ações que rendem dividendos diários." },
    { key: "fazenda", name: "Fazenda Gorillaz",  emoji: "🚜", price: 60000,  income: 6000,  sort: 10, desc: "Produz bananas e rende moedas todo dia." },
    { key: "terreno", name: "Terreno da Ilha",   emoji: "🏞️", price: 150000, income: 15000, sort: 11, desc: "Alugado por turistas ricos. Rende diariamente." },
    { key: "empresa", name: "Empresa do Murdoc", emoji: "🏢", price: 500000, income: 50000, sort: 12, desc: "A maior corporação da Ilha. Lucro diário alto." },
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
    DailyIncome: NumberLong(i.income),
    MaxQuantity: 1,
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

print(`Ativos hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

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
    print("Nada a inserir — todos os ativos já existem.");
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
print(`ativos: ${shop.countDocuments({ Category: CATEGORY })}`);
print(EJSON.stringify(shop.findOne({ Key: "empresa" }), null, 2));

quit(0);
