// seed-vehicles.js
// ============================================================================
// Popula a collection "ShopItem" com os 60 veículos (12 tipos x 5 modelos).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Requisitos: MongoDB 4.2+ e mongosh.
//
// Backup (recomendado):
//   mongodump --uri "mongodb://localhost:27017" --db gorillazbot --out ./backup
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-vehicles.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-vehicles.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-vehicles.js
//
// Idempotente: upsert por Key (reexecutar re-aplica os mesmos valores).
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

// ---- Enums (int) — espelham ShopItem.cs / CharacterProfile.cs -----------------
const CATEGORY_VEHICLE = 5;                 // ItemCategory.Vehicle
const VT = { Moto: 1, Carro: 2, Caminhonete: 3, Esportivo: 4, Caminhao: 5,
             Onibus: 6, Carreta: 7, Lancha: 8, Iate: 9, Navio: 10, Aviao: 11, Jato: 12 };
const LIC = { A: 0, B: 1, C: 2, D: 3, E: 4, Arrais: 5,
              Mestre: 6, Capitao: 7, PilotoPrivado: 8, PilotoComercial: 9, PilotoLinhaAerea: 10 };

const LICENSE_BY_TYPE = {
    [VT.Moto]: LIC.A,
    [VT.Carro]: LIC.B, [VT.Caminhonete]: LIC.B, [VT.Esportivo]: LIC.B,
    [VT.Caminhao]: LIC.C, [VT.Onibus]: LIC.D, [VT.Carreta]: LIC.E,
    [VT.Lancha]: LIC.Arrais, [VT.Iate]: LIC.Mestre, [VT.Navio]: LIC.Capitao,
    [VT.Aviao]: LIC.PilotoPrivado, [VT.Jato]: LIC.PilotoLinhaAerea
};

// ---- Catálogo: 12 tipos x 5 tiers --------------------------------------------
const vehicles = [
    // Moto (A) 101-105
    { key: "moto",            name: "Moto Street",         emoji: "🏍️", price: 15000,    type: VT.Moto,       sort: 101, desc: "Ágil nas vias da Ilha." },
    { key: "moto_2",          name: "Moto Agreste 150",    emoji: "🛵", price: 30000,    type: VT.Moto,       sort: 102, desc: "A lendária motinha de entrega, agora tunada." },
    { key: "moto_3",          name: "Moto Xtreme 300",     emoji: "🏍️", price: 60000,    type: VT.Moto,       sort: 103, desc: "Naked esportiva de média cilindrada." },
    { key: "moto_4",          name: "Moto Cruiser 750",    emoji: "🏍️", price: 120000,   type: VT.Moto,       sort: 104, desc: "Custom de estrada, cromada e imponente." },
    { key: "moto_5",          name: "Moto Hyper 1000",     emoji: "🏍️", price: 250000,   type: VT.Moto,       sort: 105, desc: "Carenagem aerodinâmica e velocidade pura." },

    // Carro Popular (B) 111-115
    { key: "carro_popular",   name: "Carro Popular",       emoji: "🚗", price: 40000,    type: VT.Carro,      sort: 111, desc: "O veículo do macaco assalariado." },
    { key: "carro_popular_2", name: "Hatch Bandeirante",   emoji: "🚗", price: 75000,    type: VT.Carro,      sort: 112, desc: "Compacto do povo, econômico no dia a dia." },
    { key: "carro_popular_3", name: "Sedan Gorila GL",     emoji: "🚗", price: 140000,   type: VT.Carro,      sort: 113, desc: "Sedã confortável para a família macaco." },
    { key: "carro_popular_4", name: "Sedan Executivo LX",  emoji: "🚙", price: 260000,   type: VT.Carro,      sort: 114, desc: "Para quem já subiu na carreira." },
    { key: "carro_popular_5", name: "Sedan Presidencial",  emoji: "🚗", price: 480000,   type: VT.Carro,      sort: 115, desc: "Teto solar, couro e muito status." },

    // Caminhonete (B) 121-125
    { key: "caminhonete",     name: "Caminhonete 4x4",              emoji: "🚙", price: 80000,  type: VT.Caminhonete, sort: 121, desc: "Toda-terreno para o trabalho pesado." },
    { key: "caminhonete_2",   name: "Picape Caçamba Sertaneja",     emoji: "🛻", price: 150000, type: VT.Caminhonete, sort: 122, desc: "A companheira de estrada do trabalhador." },
    { key: "caminhonete_3",   name: "Picape Trilha Selvagem",       emoji: "🛻", price: 280000, type: VT.Caminhonete, sort: 123, desc: "Suspensão alta para qualquer terreno." },
    { key: "caminhonete_4",   name: "Picape Blindada Urutu",        emoji: "🛻", price: 520000, type: VT.Caminhonete, sort: 124, desc: "Proteção de sobra nas vias da Ilha." },
    { key: "caminhonete_5",   name: "Picape Monstro 4x4",           emoji: "🚙", price: 900000, type: VT.Caminhonete, sort: 125, desc: "Quando o asfalto acaba, ela começa." },

    // Esportivo (B) 131-135
    { key: "carro_esportivo",   name: "Carro Esportivo",            emoji: "🏎️", price: 150000,   type: VT.Esportivo, sort: 131, desc: "Rápido e chamativo." },
    { key: "carro_esportivo_2", name: "Coupé Escorpião",            emoji: "🏎️", price: 300000,   type: VT.Esportivo, sort: 132, desc: "Linhas agressivas e ronco inconfundível." },
    { key: "carro_esportivo_3", name: "Supercarro Pampa GT",        emoji: "🏎️", price: 600000,   type: VT.Esportivo, sort: 133, desc: "0 a 100 em um piscar de olhos." },
    { key: "carro_esportivo_4", name: "Hipercarro Vulcano",         emoji: "🏎️", price: 1200000,  type: VT.Esportivo, sort: 134, desc: "Motorzão e aerofólio de fábrica." },
    { key: "carro_esportivo_5", name: "Bólido de Corrida F-Gorilla", emoji: "🏁", price: 2500000, type: VT.Esportivo, sort: 135, desc: "Direto do grid para as suas mãos." },

    // Caminhão (C) 141-145
    { key: "caminhao",   name: "Caminhão Basculante",            emoji: "🚛", price: 200000,  type: VT.Caminhao, sort: 141, desc: "Carrega bananas em escala industrial." },
    { key: "caminhao_2", name: "Caminhão Baú Cargueiro",         emoji: "🚚", price: 400000,  type: VT.Caminhao, sort: 142, desc: "Entrega encomendas por toda a Ilha." },
    { key: "caminhao_3", name: "Caminhão Graneleiro Soja",       emoji: "🚛", price: 800000,  type: VT.Caminhao, sort: 143, desc: "Feito para a safra render mais." },
    { key: "caminhao_4", name: "Caminhão Betoneira Concretus",   emoji: "🚛", price: 1500000, type: VT.Caminhao, sort: 144, desc: "Concretagem pesada sem frescura." },
    { key: "caminhao_5", name: "Caminhão Fora de Estrada Titã",  emoji: "🚜", price: 3000000, type: VT.Caminhao, sort: 145, desc: "Pneus gigantes para minas e pedreiras." },

    // Ônibus (D) 151-155
    { key: "onibus",   name: "Ônibus Urbano",                      emoji: "🚌", price: 250000,  type: VT.Onibus, sort: 151, desc: "Transporte público da Ilha." },
    { key: "onibus_2", name: "Ônibus Rodoviário Interestadual",    emoji: "🚌", price: 500000,  type: VT.Onibus, sort: 152, desc: "Viagens longas com poltronas reclináveis." },
    { key: "onibus_3", name: "Ônibus Executivo Leito",             emoji: "🚌", price: 1000000, type: VT.Onibus, sort: 153, desc: "Conforto de leito para os passageiros VIP." },
    { key: "onibus_4", name: "Ônibus Double-Decker Panorâmico",    emoji: "🚌", price: 2000000, type: VT.Onibus, sort: 154, desc: "Dois andares e vista de 360°." },
    { key: "onibus_5", name: "Ônibus Turnê Gorillaz",              emoji: "🚍", price: 4000000, type: VT.Onibus, sort: 155, desc: "O palco móvel da banda." },

    // Carreta (E) 161-165
    { key: "carreta",   name: "Carreta de Carga",       emoji: "🚚", price: 350000,  type: VT.Carreta, sort: 161, desc: "Para comboios de longa distância." },
    { key: "carreta_2", name: "Bitrem Bandeira",        emoji: "🚚", price: 700000,  type: VT.Carreta, sort: 162, desc: "Duas carretas, o dobro de carga." },
    { key: "carreta_3", name: "Rodotrem Colosso",       emoji: "🚛", price: 1400000, type: VT.Carreta, sort: 163, desc: "Sete eixos de pura capacidade." },
    { key: "carreta_4", name: "Carreta Tanque Fuel",    emoji: "🛢️", price: 2800000, type: VT.Carreta, sort: 164, desc: "Transporte de combustível a granel." },
    { key: "carreta_5", name: "Plataforma Extrema",     emoji: "🚚", price: 5500000, type: VT.Carreta, sort: 165, desc: "Para cargas que não cabem em mais nada." },

    // Lancha (Arrais) 171-175
    { key: "lancha",   name: "Lancha Gorillaz",            emoji: "🚤", price: 200000,  type: VT.Lancha, sort: 171, desc: "Para pescar no litoral." },
    { key: "lancha_2", name: "Lancha Pescadora Costeira",  emoji: "🚤", price: 400000,  type: VT.Lancha, sort: 172, desc: "Ideal para o arrastão de fim de semana." },
    { key: "lancha_3", name: "Lancha Esportiva Maré Alta", emoji: "🚤", price: 800000,  type: VT.Lancha, sort: 173, desc: "Corta as ondas na maior velocidade." },
    { key: "lancha_4", name: "Lancha de Luxo Azimute",     emoji: "🛥️", price: 1600000, type: VT.Lancha, sort: 174, desc: "Acabamento premium e som potente." },
    { key: "lancha_5", name: "Lancha Offshore Furacão",    emoji: "🚤", price: 3200000, type: VT.Lancha, sort: 175, desc: "Feita para o alto-mar e para o perrengue." },

    // Iate (Mestre) 181-185
    { key: "iate",    name: "Iate do Murdoc",            emoji: "🛥️", price: 600000,   type: VT.Iate, sort: 181, desc: "Luxo nas águas da Ilha." },
    { key: "iate_2",  name: "Iate de Cruzeiro Albatroz", emoji: "🛥️", price: 1200000,  type: VT.Iate, sort: 182, desc: "Passeios tranquilos à beira da costa." },
    { key: "iate_3",  name: "Iate de Luxo Poseidon",     emoji: "🛥️", price: 2500000,  type: VT.Iate, sort: 183, desc: "Suítes, jacuzzi e tripulação completa." },
    { key: "iate_4",  name: "Superiate Leviathan",       emoji: "🛥️", price: 5000000,  type: VT.Iate, sort: 184, desc: "Um condomínio flutuante particular." },
    { key: "iate_5",  name: "Megaiate Kraken",           emoji: "🛥️", price: 10000000, type: VT.Iate, sort: 185, desc: "O topo do mar: heliponto e adega." },

    // Navio (Capitão) 191-195
    { key: "navio",    name: "Navio Pirata",               emoji: "🚢", price: 2000000,  type: VT.Navio, sort: 191, desc: "Domine os mares." },
    { key: "navio_2",  name: "Navio de Carga Mercante",    emoji: "🚢", price: 4000000,  type: VT.Navio, sort: 192, desc: "Contêineres e mais contêineres." },
    { key: "navio_3",  name: "Transatlântico Aurora",      emoji: "🛳️", price: 8000000,  type: VT.Navio, sort: 193, desc: "Cruzeiros elegantes pelo mundo." },
    { key: "navio_4",  name: "Porta-Contêineres Titã",     emoji: "🚢", price: 16000000, type: VT.Navio, sort: 194, desc: "O gigante da logística global." },
    { key: "navio_5",  name: "Navio de Cruzeiro Odisseia", emoji: "🛳️", price: 30000000, type: VT.Navio, sort: 195, desc: "Uma cidade inteira sobre as águas." },

    // Avião (Piloto Privado) 201-205
    { key: "aviao",   name: "Avião Particular",           emoji: "✈️", price: 800000,   type: VT.Aviao, sort: 201, desc: "Cruze os céus com estilo." },
    { key: "aviao_2", name: "Táxi Aéreo Bandeirante",     emoji: "✈️", price: 1600000,  type: VT.Aviao, sort: 202, desc: "Leva e traz passageiros nas nuvens." },
    { key: "aviao_3", name: "Avião Regional Cerrado",     emoji: "✈️", price: 3200000,  type: VT.Aviao, sort: 203, desc: "Conexões entre ilhas e continente." },
    { key: "aviao_4", name: "Jato Particular Falcão",     emoji: "🛩️", price: 6500000,  type: VT.Aviao, sort: 204, desc: "Pequeno, rápido e discreto." },
    { key: "aviao_5", name: "Avião Comercial AeroGorilla", emoji: "✈️", price: 13000000, type: VT.Aviao, sort: 205, desc: "Cia aérea própria, frota própria." },

    // Jato (Piloto de Linha Aérea) 211-215
    { key: "jato",   name: "Jato Executivo",               emoji: "🛩️", price: 5000000,  type: VT.Jato, sort: 211, desc: "O ápice da aviação." },
    { key: "jato_2", name: "Jato Corporativo Sombra",      emoji: "🛩️", price: 10000000, type: VT.Jato, sort: 212, desc: "Reuniões a 12.000 pés de altitude." },
    { key: "jato_3", name: "Jato Supersônico Cometa",      emoji: "🛫", price: 20000000, type: VT.Jato, sort: 213, desc: "Rompe a barreira do som." },
    { key: "jato_4", name: "Jato de Caça Tempestade",      emoji: "🛩️", price: 40000000, type: VT.Jato, sort: 214, desc: "Manobras acrobáticas e poder de fogo." },
    { key: "jato_5", name: "Jato Presidencial Air Force G", emoji: "🛩️", price: 80000000, type: VT.Jato, sort: 215, desc: "A comitiva oficial dos Gorillaz." },
];

// ---- Pré-checagem ------------------------------------------------------------
const shop = db.getCollection("ShopItem");

const before = shop.countDocuments({ Category: CATEGORY_VEHICLE });
print(`Veículos hoje em ShopItem: ${before}`);

const unknownKeys = shop.countDocuments({
    Category: CATEGORY_VEHICLE,
    Key: { $nin: vehicles.map(v => v.key) }
});
if (unknownKeys > 0)
    print(`! ${unknownKeys} veículo(s) com Key fora deste catálogo — o script não os remove.`);

// ---- Monta os documentos -----------------------------------------------------
const ops = vehicles.map(v => {
    const doc = {
        Key: v.key,
        Name: v.name,
        Emoji: v.emoji,
        Description: v.desc,
        Price: NumberLong(v.price),
        Category: CATEGORY_VEHICLE,   // 5 = Vehicle
        Effect: 0,                    // BoostEffect.None
        DurationHours: 0,
        DailyIncome: NumberLong(0),
        MaxQuantity: 1,
        IsActive: true,
        IsPlaceholder: false,
        SortOrder: v.sort,
        RelicEffect: 0,
        RelicGame: 0,
        RelicValue: 0,
        UpgradeEffect: 0,
        UpgradeValue: 0,
        VehicleType: v.type,
        RequiredLicense: LICENSE_BY_TYPE[v.type]
    };
    return { updateOne: { filter: { Key: v.key }, update: { $set: doc }, upsert: true } };
});

const result = shop.bulkWrite(ops, { ordered: false });
print(`bulkWrite: matched=${result.matchedCount} · modified=${result.modifiedCount} · upserted=${result.upsertedCount}`);

// Alternativa "insertMany" puro (só para banco limpo; NÃO faz backfill dos 12 antigos):
// shop.insertMany(ops.map(o => o.updateOne.update.$set), { ordered: false });

// ---- Índice único (opcional) -------------------------------------------------
try {
    shop.createIndex({ Key: 1 }, { unique: true, name: "uq_ShopItem_Key" });
    print("Índice único uq_ShopItem_Key garantido.");
}
catch (e) {
    print(`! Falha ao criar índice único (há Key duplicada?): ${e.message}`);
}

// ---- Validação ---------------------------------------------------------------
const after = shop.countDocuments({ Category: CATEGORY_VEHICLE });
const withoutLicense = shop.countDocuments({ Category: CATEGORY_VEHICLE, RequiredLicense: { $exists: false } });
const byType = shop.aggregate([
    { $match: { Category: CATEGORY_VEHICLE } },
    { $group: { _id: "$VehicleType", total: { $sum: 1 } } },
    { $sort: { _id: 1 } }
]).toArray();

print("== RESULTADO ========================================================================");
print(`veículos: ${after} · sem RequiredLicense: ${withoutLicense}`);
byType.forEach(g => print(`  type ${g._id}: ${g.total}`));
print("");
print(EJSON.stringify(shop.findOne({ Key: "moto" }), null, 2));
print("");

print("Para conferir tudo:");
print("  db.ShopItem.find({ Category: 5 }).sort({ SortOrder: 1 }).forEach(d => print(d.Key, '|', d.VehicleType, '|', d.RequiredLicense))");

quit(0);