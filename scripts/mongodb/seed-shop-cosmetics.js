// seed-shop-cosmetics.js
// ============================================================================
// Popula a collection "ShopItem" com a categoria COSMETIC (ItemCategory 0),
// incluindo os itens placeholder (IsPlaceholder: true).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-shop-cosmetics.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-shop-cosmetics.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-shop-cosmetics.js
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
const CATEGORY = 0;                 // ItemCategory.Cosmetic
const BOOST_NONE = 0;               // BoostEffect.None
const UPGRADE_NONE = 0;             // UpgradeEffect.None
const RELIC_NONE = 0;               // RelicEffect.None
const GAME_ALL = 0;                 // RelicGameType.All

// ---- Catálogo -----------------------------------------------------------------
const items = [
    { key: "banana",  name: "Banana Dourada",    emoji: "🍌", price: 1000,  sort: 1, placeholder: false, desc: "Item de coleção do Gorillaz." },
    { key: "bone",    name: "Boné do Gorillaz",  emoji: "🧢", price: 2500,  sort: 2, placeholder: false, desc: "Um boné exclusivo de coleção." },
    { key: "trofeu",  name: "Troféu Prime",      emoji: "🏆", price: 5000,  sort: 3, placeholder: false, desc: "Mostre que você é o macaco alfa." },
    { key: "camisa",  name: "Camisa 2D",         emoji: "🎤", price: 7500,  sort: 4, placeholder: false, desc: "A camiseta oficial do vocalista." },
    { key: "mascara", name: "Máscara de Murdoc", emoji: "🎭", price: 12000, sort: 5, placeholder: false, desc: "A máscara do baixista, peça rara." },

    { key: "segredo", name: "??? Ídolo Secreto",  emoji: "🗿", price: 0, sort: 13, placeholder: true, desc: "Algo lendário está para chegar..." },
    { key: "bau",     name: "??? Baú do Oceano",  emoji: "🗝️", price: 0, sort: 14, placeholder: true, desc: "Abaixo das ondas da Ilha..." },
    { key: "evento",  name: "??? Item de Evento", emoji: "🎆", price: 0, sort: 15, placeholder: true, desc: "Reservado para um evento especial..." },
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
    MaxQuantity: 0,
    IsActive: true,
    IsPlaceholder: i.placeholder === true,
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

print(`Cosméticos hoje em ShopItem: ${shop.countDocuments({ Category: CATEGORY })}`);

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
    print("Nada a inserir — todos os cosméticos já existem.");
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
const total = shop.countDocuments({ Category: CATEGORY });
const placeholders = shop.countDocuments({ Category: CATEGORY, IsPlaceholder: true });
print("== RESULTADO ========================================================================");
print(`cosméticos: ${total} (placeholders: ${placeholders})`);
print(EJSON.stringify(shop.findOne({ Key: "mascara" }), null, 2));

quit(0);
