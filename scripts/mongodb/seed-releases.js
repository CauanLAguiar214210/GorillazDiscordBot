// seed-releases.js
// ============================================================================
// Popula a collection "ReleaseNote" com as releases/novidades do bot.
// O bot anuncia as releases pendentes (AnnouncedAt: null) ao subir, nos canais
// configurados via /config release-canal (ou manualmente com /release anunciar).
// Executa DIRETO no mongosh (não passa pela aplicação).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-releases.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-releases.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-releases.js
//
// Idempotente E CORRIGÍVEL: rerun atualiza título/descrição/features pelo
// Version, mas NUNCA reanuncia (AnnouncedAt só é definido na inserção).
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

// ---- Enum (int) — espelha ReleaseFeatureType.cs -------------------------------
const FEATURE = { New: 0, Improvement: 1, Fix: 2 };

// ---- Releases -----------------------------------------------------------------
const releases = [
    {
        version: "v1.1.0",
        title: "Crimes, Trabalhos e Loja turbinada",
        description: "A maior atualização do bot até agora: sistema de crimes, loja com armas e equipamentos, trabalho com licenças e muito mais.",
        publishedAt: "2026-09-18T00:00:00.000Z",
        features: [
            { type: FEATURE.New,         title: "Sistema de crimes",           desc: "`/crime furto` nas ruas e `/crime roubar` outros jogadores, com fiança e prisão" },
            { type: FEATURE.New,         title: "Arsenal e equipamentos",      desc: "Equipe arma e equipamento para ganhar bônus de ataque e defesa nos crimes" },
            { type: FEATURE.New,         title: "Loja expandida",              desc: "Armas, equipamentos e pets agora disponíveis na loja, com bônus in-game" },
            { type: FEATURE.New,         title: "Sistema de trabalho",         desc: "Subempregos, profissões, provas de licença e diplomas em `/trabalho`" },
            { type: FEATURE.New,         title: "Escola e escolaridade",       desc: "Provas de matemática para subir de nível escolar em `/ensino`" },
            { type: FEATURE.New,         title: "Veículos e habilitação",      desc: "Licenças terrestre, marítima e aérea com provas, garagem e veículo equipável" },
            { type: FEATURE.New,         title: "Banco completo",              desc: "CDB diário, poupança com streak e mercado de ativos de renda em `/banco`" },
            { type: FEATURE.New,         title: "Cassino via LuckyMonkey",     desc: "Blackjack, roleta, caça-níquel, poker e mais jogos com botões" },
            { type: FEATURE.New,         title: "Contas vinculadas",           desc: "Vincule suas alts e unifique a economia da sua conta" },
            { type: FEATURE.New,         title: "Moderação com avisos",        desc: "Avisos registrados, timeout, banimentos e limpeza de mensagens" },

            { type: FEATURE.Improvement, title: "Trabalhos rebalanceados",     desc: "Pagamentos e cooldowns ajustados para uma economia mais equilibrada" },
            { type: FEATURE.Improvement, title: "Canais de voz dinâmicos",     desc: "Canal criador cria um canal privado com o nome de quem entrou" },
            { type: FEATURE.Improvement, title: "Interações por trigger",      desc: "Respostas automáticas com texto, GIF, áudio ou vídeo em `/interacao`" },
            { type: FEATURE.Improvement, title: "Ranking e classes econômicas", desc: "Ranking de patrimônio com tiers e Hall da Fama" },

            { type: FEATURE.Fix,         title: "Registro de slash commands",  desc: null },
            { type: FEATURE.Fix,         title: "Workflow de deploy na AWS",   desc: null },
        ]
    },
];

// ---- Monta os documentos ------------------------------------------------------
const docs = releases.map(r => ({
    Version: r.version,
    Title: r.title,
    Description: r.description || null,
    PublishedAt: new Date(r.publishedAt),
    Features: r.features.map(f => ({
        Type: f.type,
        Title: f.title,
        Description: f.desc || null
    }))
}));

// ---- Upsert idempotente -------------------------------------------------------
const col = db.getCollection("ReleaseNote");

print(`Releases hoje em ReleaseNote: ${col.countDocuments({})}`);

const ops = docs.map(d => ({
    updateOne: {
        filter: { Version: d.Version },
        update: { $set: d, $setOnInsert: { AnnouncedAt: null } },
        upsert: true
    }
}));

const result = col.bulkWrite(ops, { ordered: false });
print(`bulkWrite: matched=${result.matchedCount} · modified=${result.modifiedCount} · upserted=${result.upsertedCount}`);

// ---- Índice único -------------------------------------------------------------
try {
    col.createIndex({ Version: 1 }, { unique: true, name: "uq_ReleaseNote_Version" });
    print("Índice único uq_ReleaseNote_Version garantido.");
}
catch (e) {
    print(`! Falha ao criar índice único (há Version duplicada?): ${e.message}`);
}

// ---- Validação ----------------------------------------------------------------
const total = col.countDocuments({});
const pending = col.countDocuments({ AnnouncedAt: null });
print("== RESULTADO ========================================================================");
print(`releases: ${total} · pendentes de anúncio: ${pending}`);
print("(pendentes são anunciadas no próximo boot do bot, nos canais de /config release-canal —");
print(" ou manualmente com /release anunciar)");
print(EJSON.stringify(col.findOne({ Version: "v1.1.0" }), null, 2));

quit(0);
