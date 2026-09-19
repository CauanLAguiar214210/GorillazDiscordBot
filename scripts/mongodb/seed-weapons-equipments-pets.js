// seed-weapons-equipments-pets.js
// ============================================================================
// Popula a collection "ShopItem" com Armas (ItemCategory 7), Equipamentos (ItemCategory 8)
// e novos Pets de crime e defesa (ItemCategory 4).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-weapons-equipments-pets.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-weapons-equipments-pets.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-weapons-equipments-pets.js
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
const CATEGORY_PET = 4;        // ItemCategory.Pet
const CATEGORY_WEAPON = 7;     // ItemCategory.Weapon
const CATEGORY_EQUIPMENT = 8;  // ItemCategory.Equipment

const BOOST_NONE = 0;
const RELIC_NONE = 0;
const GAME_ALL = 0;
const MAX_PET_LEVEL = 10;

const WEAPON = { None: 0, Branca: 1, Fogo: 2, Pesada: 3, Choque: 4 };
const EQUIP = {
    None: 0, Luvas: 1, Mascara: 2, Lockpick: 3, PeDeCabra: 4, Colete: 5, Alarme: 6,
    InibidorEmp: 7, Sapatilhas: 8, Mochila: 9, Scanner: 10, Fumigeno: 11,
    Camera4k: 12, Cofre: 13, Tinta: 14, Cerca: 15, Medkit: 16
};

const UPGRADE = {
    None: 0, Daily: 1, Work: 2, Rob: 3, AssetIncome: 4, Savings: 5,
    ManobristaBase: 6, ManobristaVagas: 7, CrimeDefesa: 8, CrimeFurto: 9
};

// ---- Catálogo Completo: Armas, Equipamentos e Pets ----------------------------
const items = [
    // ─── PETS DE CRIME & DEFESA (ItemCategory.Pet = 4) ─────────────────────────
    { key: "pet_batedor", name: "Macaco-Batedor", emoji: "🥷", price: 25000, sort: 50, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.Rob, upVal: 3, desc: "+3% nas moedas roubadas por nível. Máximo de 2 tipos de pets." },
    { key: "pet_dobermann", name: "Dobermann de Guarda", emoji: "🐕", price: 35000, sort: 84, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeDefesa, upVal: 3, desc: "+3% por nível de chance de afugentar invasores. Máx 2 pets." },
    { key: "pet_guaxinim", name: "Guaxinim Gatuno", emoji: "🦝", price: 20000, sort: 85, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeFurto, upVal: 4, desc: "+4% por nível nas moedas de furtos sem cooldown. Máx 2 pets." },
    { key: "pet_falcao", name: "Falcão Vigilante", emoji: "🦅", price: 45000, sort: 86, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeDefesa, upVal: 2, desc: "+2% por nível de fuga e visão de emboscadas. Máx 2 pets." },
    { key: "pet_serpente", name: "Serpente Venenosa", emoji: "🐍", price: 30000, sort: 87, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeDefesa, upVal: 3, desc: "+3% por nível de dissuasão defensiva. Máx 2 pets." },
    { key: "pet_papagaio", name: "Papagaio Informante", emoji: "🦜", price: 16000, sort: 88, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeFurto, upVal: 2, desc: "+2% por nível de inteligência e vigilância. Máx 2 pets." },
    { key: "pet_jacare", name: "Jacaré do Lago", emoji: "🐊", price: 120000, sort: 89, cat: CATEGORY_PET, maxQty: MAX_PET_LEVEL, upEffect: UPGRADE.CrimeDefesa, upVal: 5, desc: "+5% por nível de defesa máxima de magnata. Máx 2 pets." },

    // ─── ARMAS (ItemCategory.Weapon = 7) ───────────────────────────────────────
    { key: "canivete", name: "Canivete Butterfly", emoji: "🔪", price: 2500, sort: 51, cat: CATEGORY_WEAPON, wType: WEAPON.Branca, bonus: 8, def: 5, cap: 2000, desc: "Arma branca ágil. +8% de sucesso em assaltos." },
    { key: "taco", name: "Taco de Beisebol", emoji: "🏏", price: 5000, sort: 52, cat: CATEGORY_WEAPON, wType: WEAPON.Branca, bonus: 12, def: 10, cap: 4000, desc: "Arma de impacto. +12% de sucesso em assaltos e defesa." },
    { key: "taser", name: "Taser de Choque", emoji: "⚡", price: 9000, sort: 53, cat: CATEGORY_WEAPON, wType: WEAPON.Choque, bonus: 15, def: 30, cap: 6000, desc: "Defesa elétrica. +15% de sucesso; 30% chance de paralisar assaltantes." },
    { key: "pistola", name: "Pistola 9mm", emoji: "🔫", price: 25000, sort: 54, cat: CATEGORY_WEAPON, wType: WEAPON.Fogo, bonus: 22, def: 25, cap: 15000, desc: "Arma de fogo portátil. +22% de sucesso em assaltos e defesa armada." },
    { key: "escopeta", name: "Escopeta Calibre 12", emoji: "💥", price: 60000, sort: 55, cat: CATEGORY_WEAPON, wType: WEAPON.Fogo, bonus: 32, def: 35, cap: 35000, desc: "Alto poder de fogo. +32% de sucesso e defesa." },
    { key: "fuzil", name: "Fuzil Tático", emoji: "💣", price: 140000, sort: 56, cat: CATEGORY_WEAPON, wType: WEAPON.Pesada, bonus: 45, def: 45, cap: 80000, desc: "Armamento militar pesado. +45% de sucesso em grandes assaltos." },
    { key: "machadinha", name: "Machadinha Tática", emoji: "🪓", price: 7500, sort: 63, cat: CATEGORY_WEAPON, wType: WEAPON.Branca, bonus: 14, def: 8, cap: 5000, desc: "Arma branca silenciosa. +14% em assaltos." },
    { key: "pistola_silenciosa", name: "Pistola Silenciada", emoji: "🎯", price: 38000, sort: 64, cat: CATEGORY_WEAPON, wType: WEAPON.Fogo, bonus: 24, def: 15, cap: 18000, desc: "Disparo sem ruído. +24% sucesso, reduz suspeita." },
    { key: "besta", name: "Besta Tática", emoji: "🏹", price: 48000, sort: 65, cat: CATEGORY_WEAPON, wType: WEAPON.Branca, bonus: 28, def: 20, cap: 25000, desc: "Perfuração de blindagem. +28% de sucesso em assaltos." },
    { key: "mp5", name: "Submetralhadora MP5", emoji: "🔫", price: 85000, sort: 66, cat: CATEGORY_WEAPON, wType: WEAPON.Fogo, bonus: 36, def: 28, cap: 45000, desc: "Rajada silenciada. +36% sucesso, alto poder de fogo." },
    { key: "motosserra", name: "Motosserra do Murdoc", emoji: "🪚", price: 30000, sort: 67, cat: CATEGORY_WEAPON, wType: WEAPON.Pesada, bonus: 20, def: 10, cap: 12000, desc: "Intimidação brutal da banda. +20% sucesso." },
    { key: "gl", name: "Lança-Granadas", emoji: "💥", price: 180000, sort: 68, cat: CATEGORY_WEAPON, wType: WEAPON.Pesada, bonus: 48, def: 40, cap: 100000, desc: "Poder destrutivo massivo. +48% sucesso, intimidação pesada." },
    { key: "sniper", name: "Fuzil Sniper", emoji: "🎯", price: 250000, sort: 69, cat: CATEGORY_WEAPON, wType: WEAPON.Pesada, bonus: 52, def: 30, cap: 120000, desc: "Tiro de longa distância. +52% de sucesso no assalto." },
    { key: "minigun", name: "Minigun Dourada", emoji: "👑", price: 1000000, sort: 70, cat: CATEGORY_WEAPON, wType: WEAPON.Pesada, bonus: 60, def: 60, cap: 300000, desc: "Armamento lendário da Ilha. +60% sucesso e defesa máxima." },
    { key: "spray", name: "Spray de Pimenta", emoji: "🌶️", price: 3500, sort: 71, cat: CATEGORY_WEAPON, wType: WEAPON.Choque, bonus: 5, def: 20, cap: 1000, desc: "Autodefesa civil. 40% de chance de afugentar ladrões." },
    { key: "cassetete", name: "Cassetete de Titânio", emoji: "🦯", price: 6000, sort: 72, cat: CATEGORY_WEAPON, wType: WEAPON.Branca, bonus: 10, def: 18, cap: 2500, desc: "Impacto defensivo rápido. +18% de defesa armada." },
    { key: "taser_x2", name: "Taser Duplo X2", emoji: "⚡", price: 18000, sort: 73, cat: CATEGORY_WEAPON, wType: WEAPON.Choque, bonus: 16, def: 40, cap: 7500, desc: "Paralisia elétrica dupla. 40% de defesa contra invasores." },

    // ─── EQUIPAMENTOS (ItemCategory.Equipment = 8) ────────────────────────────
    { key: "luvas", name: "Luvas de Pelica", emoji: "🧤", price: 4000, sort: 57, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Luvas, bonus: 20, def: 0, desc: "Discrição máxima. +20% de sucesso em furtos sem cooldown." },
    { key: "balaclava", name: "Máscara Balaclava", emoji: "🎭", price: 6500, sort: 58, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Mascara, bonus: 0, def: 0, desc: "Anonimato. Oculta sua identidade caso seja pego ou cometa crimes." },
    { key: "lockpick", name: "Kit de Gazuas", emoji: "🗝️", price: 3500, sort: 59, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Lockpick, bonus: 15, def: 0, desc: "Ferramenta para arrombamento silencioso. +15% de sucesso em invasões." },
    { key: "pedecabra", name: "Pé de Cabra", emoji: "🦯", price: 8000, sort: 60, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.PeDeCabra, bonus: 25, def: 0, desc: "Alavanca de aço. +25% de moedas obtidas em arrombamentos." },
    { key: "colete", name: "Colete Kevlar", emoji: "🦺", price: 20000, sort: 61, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Colete, bonus: 0, def: 50, desc: "Proteção pessoal. Reduz o dinheiro que conseguem roubar de você pela metade." },
    { key: "alarme", name: "Alarme Residencial", emoji: "🚨", price: 15000, sort: 62, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Alarme, bonus: 0, def: 30, desc: "Dispara no flagrante: dobra a multa que o invasor paga a você." },
    { key: "emp", name: "Inibidor EMP", emoji: "📡", price: 22000, sort: 74, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.InibidorEmp, bonus: 15, def: 0, desc: "Dispositivo eletrônico: anula alarmes residenciais no assalto." },
    { key: "sapatilhas", name: "Sapatilhas Silenciosas", emoji: "🥷", price: 8500, sort: 75, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Sapatilhas, bonus: 15, def: 0, desc: "Passos amortecidos. +15% de sucesso em furtos sem cooldown." },
    { key: "mochila", name: "Mochila Tática", emoji: "🎒", price: 35000, sort: 76, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Mochila, bonus: 25, def: 0, desc: "Compartimentos reforçados. +50% de moedas roubadas." },
    { key: "scanner", name: "Scanner Policial", emoji: "📻", price: 45000, sort: 77, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Scanner, bonus: 10, def: 10, desc: "Intercepta rádio da polícia. Reduz multas de flagrante." },
    { key: "fumigeno", name: "Granada de Fumaça", emoji: "💨", price: 12000, sort: 78, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Fumigeno, bonus: 20, def: 15, desc: "Fumígeno tático de escape instantâneo." },
    { key: "camera4k", name: "Câmera Noturna 4K", emoji: "📹", price: 18000, sort: 79, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Camera4k, bonus: 0, def: 25, desc: "Vigilância perimetral. Revela invasores mascarados." },
    { key: "cofre", name: "Cofre Fundo Falso", emoji: "🏦", price: 50000, sort: 80, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Cofre, bonus: 0, def: 40, desc: "Esconderijo secreto. Protege 40% da carteira contra assaltos." },
    { key: "tinta", name: "Tinta Anti-Furto", emoji: "💣", price: 14000, sort: 81, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Tinta, bonus: 0, def: 30, desc: "Armadilha química: queima e destrói 50% das moedas se te roubarem." },
    { key: "cerca", name: "Cerca Eletrificada", emoji: "⚡", price: 28000, sort: 82, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Cerca, bonus: 0, def: 35, desc: "Defesa de perímetro: desestimula e repele invasores." },
    { key: "medkit", name: "Kit Médico", emoji: "🩺", price: 10000, sort: 83, cat: CATEGORY_EQUIPMENT, eqType: EQUIP.Medkit, bonus: 0, def: 15, desc: "Primeiros socorros para emergências em combate." }
];

// ---- Monta os documentos -----------------------------------------------------
const docs = items.map(i => ({
    Key: i.key,
    Name: i.name,
    Emoji: i.emoji,
    Description: i.desc,
    Price: NumberLong(i.price),
    Category: i.cat,
    Effect: BOOST_NONE,
    DurationHours: 0,
    DailyIncome: NumberLong(0),
    MaxQuantity: i.maxQty || 1,
    IsActive: true,
    IsPlaceholder: false,
    SortOrder: i.sort,
    RelicEffect: RELIC_NONE,
    RelicGame: GAME_ALL,
    RelicValue: 0,
    UpgradeEffect: i.upEffect || 0,
    UpgradeValue: i.upVal || 0,
    VehicleType: 0,
    RequiredLicense: null,
    WeaponType: i.wType || 0,
    EquipmentType: i.eqType || 0,
    CrimeBonusPercent: i.bonus || 0,
    CrimeDefensePercent: i.def || 0,
    CrimeMaxStealBonus: NumberLong(i.cap || 0)
}));

// ---- Pré-checagem / deduplicação ---------------------------------------------
const shop = db.getCollection("ShopItem");

print(`Total atual de itens em ShopItem: ${shop.countDocuments()}`);

const existingKeys = new Set(
    shop.find({ Key: { $in: docs.map(d => d.Key) } }, { Key: 1 }).toArray().map(d => d.Key)
);
const toInsert = docs.filter(d => !existingKeys.has(d.Key));
print(`Catálogo novo: ${docs.length} itens.`);
print(`Já existem na base (pulados): ${existingKeys.size}.`);
print(`Novos itens a inserir: ${toInsert.length}.`);

// ---- insertMany --------------------------------------------------------------
if (toInsert.length > 0) {
    const result = shop.insertMany(toInsert, { ordered: false });
    print(`insertMany concluído com sucesso: ${result.insertedIds ? Object.keys(result.insertedIds).length : toInsert.length} inseridos.`);
} else {
    print("Todos os itens já existem no banco de dados.");
}

// ---- Exibe resumo das categorias ---------------------------------------------
print("\n--- Resumo Atualizado de Itens ---");
print(`Pets: ${shop.countDocuments({ Category: CATEGORY_PET })}`);
print(`Armas: ${shop.countDocuments({ Category: CATEGORY_WEAPON })}`);
print(`Equipamentos: ${shop.countDocuments({ Category: CATEGORY_EQUIPMENT })}`);
print("----------------------------------");
