// normalize-guilds.js
// ============================================================================
// Migra as collections legadas (GuildWelcomeSettings, GuildPrefixSettings,
// GuildVoiceSettings) para a nova collection "Guild" — um documento por servidor
// com settings embutidos (Info, Prefix, Welcome, VoiceChannels).
//
// Requisitos: MongoDB 4.2+ (usa $merge) e mongosh.
//
// Backup (recomendado antes de rodar):
//   mongodump --uri "mongodb://localhost:27017" --db gorillazbot --out ./backup
//   # ou via container:
//   docker exec -it gorillaz-mongodb mongodump --db gorillazbot --out /data/db/backup
//
// Como rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/normalize-guilds.js
//
// Como rodar (docker-compose):
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/normalize-guilds.js
//
// Com connection string (env ou 1º argumento URI):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/normalize-guilds.js
//   mongosh scripts/mongodb/normalize-guilds.js "mongodb+srv://user:pass@host/gorillazbot"
// ============================================================================

const legacyCollections = ["GuildWelcomeSettings", "GuildPrefixSettings", "GuildVoiceSettings"];
const targetCollection = "Guild";
const defaultDbName = "gorillazbot";

const extractDatabaseName = (uri) => {
    const rest = String(uri).replace(/^mongodb(\+srv)?:\/\//i, "");
    const slash = rest.indexOf("/");
    if (slash === -1) return defaultDbName;
    const path = rest.substring(slash + 1);
    const q = path.indexOf("?");
    const name = (q === -1 ? path : path.substring(0, q)).trim();
    return name || defaultDbName;
};

const maskUri = (uri) => {
    try {
        const parsed = new URL(uri);
        if (parsed.password) {
            parsed.password = "***";
        }
        return parsed.href;
    }
    catch {
        return uri;
    }
};

// Conexão: env MONGODB_CONNECTION_STRING > URI nos args > mongosh já conectado (db global).
const connectionString =
    process.env.MONGODB_CONNECTION_STRING ||
    process.argv.find((arg) => /^mongodb(\+srv)?:\/\//i.test(arg)) ||
    null;

if (connectionString) {
    const databaseName = extractDatabaseName(connectionString);
    db = connect(connectionString).getDB(databaseName);
    print(`Conectado em ${maskUri(connectionString)} (db: ${databaseName})`);
    print("");
}

const exists = (name) => db.getCollectionNames().includes(name);
const count = (name) => (exists(name) ? db.getCollection(name).countDocuments() : 0);

print("== PRÉ-CHECAGEM ========================================================================");

const present = [];
for (const coll of legacyCollections) {
    if (exists(coll)) {
        present.push(coll);
        const sample = db.getCollection(coll).findOne();
        print(`- ${coll}: ${count(coll)} doc(s)  amostra -> ${JSON.stringify(sample)}`);
    }
    else {
        print(`! ${coll}: collection não encontrada — pulando.`);
    }
}

if (present.length === 0) {
    print("Nenhuma collection legada encontrada. Nada a fazer.");
    quit(0);
}

if (exists(targetCollection)) {
    print(`! A collection '${targetCollection}' JÁ existe (${count(targetCollection)} doc(s)). ` +
        "O script fará merge/upsert por _id — documentos existentes terão campos mesclados.");
}
else {
    print(`A collection '${targetCollection}' será criada pelo primeiro $merge.`);
}

print("");

// =============================================================================================
// Passo 1 — Welcome: cria o doc base (Info + Welcome).  quandoMatched:"merge" preserva
// documentos existentes (ex.: guilds que só tinham prefix ou voice).
// =============================================================================================
if (exists("GuildWelcomeSettings")) {
    print("== Passo 1/3 — GuildWelcomeSettings -> Guild (Info + Welcome) ========================");

    db.GuildWelcomeSettings.aggregate([
        {
            $set: {
                Info: {},
                Welcome: {
                    WelcomeChannelId: "$WelcomeChannelId",
                    GoodbyeChannelId: "$GoodbyeChannelId",
                    WelcomeMessage: "$WelcomeMessage",
                    GoodbyeMessage: "$GoodbyeMessage",
                    WelcomeEnabled: "$WelcomeEnabled",
                    GoodbyeEnabled: "$GoodbyeEnabled"
                }
            }
        },
        { $project: { _id: 1, Info: 1, Welcome: 1 } },
        { $merge: { into: targetCollection, on: "_id", whenMatched: "merge", whenNotMatched: "insert" } }
    ]);

    print(`  -> ${count(targetCollection)} doc(s) em '${targetCollection}'.`);
    print("");
}

// =============================================================================================
// Passo 2 — Prefix.
// =============================================================================================
if (exists("GuildPrefixSettings")) {
    print("== Passo 2/3 — GuildPrefixSettings -> Guild (Prefix) =================================");

    db.GuildPrefixSettings.aggregate([
        { $set: { Prefix: { Prefix: "$Prefix" } } },
        { $project: { _id: 1, Prefix: 1 } },
        { $merge: { into: targetCollection, on: "_id", whenMatched: "merge", whenNotMatched: "insert" } }
    ]);

    print(`  -> ${count(targetCollection)} doc(s) em '${targetCollection}'.`);
    print("");
}

// =============================================================================================
// Passo 3 — Voice: um canal por guild vira array VoiceChannels (1..N).
// NameTemplate/UserLimit/CategoryId ficam ausentes; defaults aplicados no read pelo bot.
// =============================================================================================
if (exists("GuildVoiceSettings")) {
    print("== Passo 3/3 — GuildVoiceSettings -> Guild (VoiceChannels) ===========================");

    db.GuildVoiceSettings.aggregate([
        {
            $set: {
                VoiceChannels: [
                    { CreatorChannelId: "$CreatorChannelId", Enabled: "$Enabled" }
                ]
            }
        },
        { $project: { _id: 1, VoiceChannels: 1 } },
        { $merge: { into: targetCollection, on: "_id", whenMatched: "merge", whenNotMatched: "insert" } }
    ]);

    print(`  -> ${count(targetCollection)} doc(s) em '${targetCollection}'.`);
    print("");
}

// =============================================================================================
// Validação final.
// =============================================================================================
print("== RESULTADO ============================================================================");

const guildTotal = count(targetCollection);
const withWelcome = db.getCollection(targetCollection).countDocuments({ Welcome: { $exists: true } });
const withPrefix = db.getCollection(targetCollection).countDocuments({ Prefix: { $exists: true } });
const withVoice = db.getCollection(targetCollection).countDocuments({ VoiceChannels: { $exists: true, $ne: [] } });
const withInfo = db.getCollection(targetCollection).countDocuments({ Info: { $exists: true } });

print(`- '${targetCollection}': ${guildTotal} doc(s)`);
print(`  docs com Welcome..........: ${withWelcome}`);
print(`  docs com Prefix...........: ${withPrefix}`);
print(`  docs com VoiceChannels....: ${withVoice}`);
print(`  docs com Info.............: ${withInfo}`);

const sample = db.getCollection(targetCollection).findOne();
print(`- exemplo migrado: ${JSON.stringify(sample)}`);

print("");
print("Coleções legadas foram preservadas. Se estiver tudo certo, remova-as manualmente:");
print(`  db.GuildWelcomeSettings.drop(); db.GuildPrefixSettings.drop(); db.GuildVoiceSettings.drop();`);

quit(0);