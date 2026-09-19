// seed-releases-catalogo.js
// ============================================================================
// Catálogo COMPLETO das funcionalidades do bot, em 3 releases temáticas:
//   v1.1.0 — Economia, Trabalho e Cassino
//   v1.1.1 — Loja, Perfil e Comunidade
//   v1.1.2 — Moderação e Qualidade de Vida
//
// O bot anuncia as releases pendentes (AnnouncedAt: null) ao subir, em ordem de
// publicação — 3 mensagens, uma por release — nos canais configurados via
// /config release-canal (ou manualmente com /release anunciar <versao>).
//
// Relação com seed-releases.js: este script SUBSTITUI o conteúdo da v1.1.0 do
// seed antigo (mesmo Version ⇒ o upsert corrige o texto sem reanunciar).
// Rode apenas este; se o antigo já foi aplicado, nada é reanunciado
// (AnnouncedAt é preservado pelo $setOnInsert).
//
// Rodar (local):
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-releases-catalogo.js
//
// Docker:
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-releases-catalogo.js
//
// Com connection string (env):
//   $env:MONGODB_CONNECTION_STRING="mongodb://user:pass@host:27017/gorillazbot"
//   mongosh scripts/mongodb/seed-releases-catalogo.js
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
        title: "Economia, Trabalho e Cassino",
        description: "O coração da jogatina: carteira e transferências, banco com rendimentos, crimes, profissões, escola, veículos e os 14 jogos do cassino.",
        publishedAt: "2026-09-18T00:00:00.000Z",
        features: [
            { type: FEATURE.New, title: "Carteira e pagamentos", desc: "`/carteira` mostra saldo e envia moedas; `daily` dá o bônus diário" },
            { type: FEATURE.New, title: "Banco completo",        desc: "`/banco`: CDB diário, saque, depósito e extrato de transações" },
            { type: FEATURE.New, title: "Mercado de ativos",     desc: "`/banco ativos`: compre e venda cotas pelo preço do dia" },
            { type: FEATURE.New, title: "Sistema de crimes",     desc: "`/crime furto`, `/crime roubar` e fiança em `/crime fianca`" },
            { type: FEATURE.New, title: "Arsenal",               desc: "`/crime arsenal`: arma e proteção para bônus nos crimes" },
            { type: FEATURE.New, title: "Trabalho e profissões", desc: "`/trabalho`: subempregos, provas (2 de 3) e diplomas" },
            { type: FEATURE.New, title: "Escola e escolaridade", desc: "`/ensino prova` sobe seu nível com provas de matemática" },
            { type: FEATURE.New, title: "Veículos",              desc: "licenças por grupo, `/garagem` e `/veiculo dirigir`" },
            { type: FEATURE.New, title: "Cassino completo",      desc: "14 jogos: `/cassino blackjack`, roleta, poker, minas e mais" },
            { type: FEATURE.New, title: "Contas vinculadas",     desc: "`contas vincular`: una suas alts e compartilhe a economia" },

            { type: FEATURE.Improvement, title: "Ranking e classes",        desc: "`ranking`, `tiers` e `raldafama`: de 🕳️ Miserável a 👑 Magnata" },
            { type: FEATURE.Improvement, title: "Trabalhos rebalanceados",  desc: "pagamentos e cooldowns ajustados para uma economia equilibrada" },
        ]
    },
    {
        version: "v1.1.1",
        title: "Loja, Perfil e Comunidade",
        description: "Loja expandida com armas e pets, perfil do personagem, boas-vindas configuráveis e a vida social do servidor.",
        publishedAt: "2026-09-18T00:05:00.000Z",
        features: [
            { type: FEATURE.New, title: "Loja expandida",         desc: "armas, equipamentos e pets: `/loja`, `/comprar`, `/inventario` e `/vender`" },
            { type: FEATURE.New, title: "Boosts e consumíveis",   desc: "`/usar` ativa boosts comprados na loja" },
            { type: FEATURE.New, title: "Relógios de prestígio",  desc: "`/equipar` e `/desequipar`: bônus nos jogos do cassino" },
            { type: FEATURE.New, title: "Perfil do personagem",   desc: "`/perfil ver`: escolaridade, CNH, diplomas, veículo, saldo e classe" },
            { type: FEATURE.New, title: "Boas-vindas e despedidas", desc: "mensagens e canais configuráveis em `/config boasvindas-*`" },

            { type: FEATURE.Improvement, title: "Canais de voz dinâmicos", desc: "entre no canal criador e ganhe uma sala privada com seu nome" },
            { type: FEATURE.Improvement, title: "Interações por trigger",  desc: "`/interacao`: respostas com texto, GIF, áudio ou vídeo" },
        ]
    },
    {
        version: "v1.1.2",
        title: "Moderação e Qualidade de Vida",
        description: "Moderação com avisos registrados, central de ajuda, comandos de utilidade e diversão e a configuração completa do servidor.",
        publishedAt: "2026-09-18T00:10:00.000Z",
        features: [
            { type: FEATURE.New, title: "Moderação completa",     desc: "`limpar`, `expulsar`, `banir`, `timeout`, `trancar` e mais (slash e prefixo)" },
            { type: FEATURE.New, title: "Sistema de avisos",      desc: "`avisar`, `avisos` e `removeaviso`: histórico de infrações por membro" },
            { type: FEATURE.New, title: "Central de ajuda",       desc: "`/ajuda`: menu com todos os comandos por categoria" },
            { type: FEATURE.New, title: "Configuração do servidor", desc: "prefixo, voz, boas-vindas e status em `/config`" },
            { type: FEATURE.New, title: "Utilidades",             desc: "`userinfo`, `serverinfo`, `avatar`, `timer`, `random`, `horario` e mais" },
            { type: FEATURE.New, title: "Diversão",               desc: "`8ball`, `dado`, `flip`, `gorila` e `/ping`" },
            { type: FEATURE.New, title: "Canal de novidades",     desc: "o bot anuncia updates: `/release listar` + `/config release-canal`" },

            { type: FEATURE.Fix, title: "Registro de slash commands", desc: null },
            { type: FEATURE.Fix, title: "Workflow de deploy na AWS",  desc: null },
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

// ---- Simulação dos limites do anúncio (ReleaseEmbedBuilder) -------------------
// O anúncio mostra no máximo 12 itens e ~1000 caracteres por seção.
const groupTitle = (type) => type === FEATURE.New ? "✨ Novidades"
    : type === FEATURE.Improvement ? "🔧 Melhorias"
    : "🐛 Correções";

let contentIssues = 0;
for (const r of releases) {
    for (const type of [FEATURE.New, FEATURE.Improvement, FEATURE.Fix]) {
        const items = r.features.filter((f) => f.type === type);
        if (items.length === 0) continue;

        const lines = items.slice(0, 12).map((f) =>
            f.desc ? `• **${f.title}** — ${f.desc}` : `• **${f.title}**`);
        const chars = lines.join("\n").length;
        const overflow = items.length > 12 || chars > 1000;
        if (overflow) contentIssues++;

        print(`${overflow ? "!" : "OK"} ${r.version} · ${groupTitle(type)}: ${items.length} item(ns), ${chars} chars${overflow ? " — SERIA CORTADO no anúncio!" : ""}`);
    }
}
if (contentIssues > 0)
    print(`! ${contentIssues} seção(ões) fora dos limites do anúncio — reduza os textos antes de rodar em produção.`);

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
const versions = releases.map(r => r.version);
const total = col.countDocuments({});
const pending = col.countDocuments({ Version: { $in: versions }, AnnouncedAt: null });

print("== RESULTADO ========================================================================");
print(`releases no banco: ${total} · destas, pendentes de anúncio: ${pending}`);
print("(o bot anuncia até 20 pendentes por boot, em ordem de publicação — uma mensagem por");
print(" release — nos canais de /config release-canal; ou manualmente com /release anunciar)");
print("-------------------------------------------------------------------------------------");
for (const v of versions) {
    const doc = col.findOne({ Version: v }, { Version: 1, Title: 1, PublishedAt: 1, AnnouncedAt: 1, Features: 1 });
    if (!doc) continue;
    const status = doc.AnnouncedAt == null ? "📌 pendente" : `✅ anunciada em ${doc.AnnouncedAt.toISOString()}`;
    print(`${doc.Version} — ${doc.Title} · ${doc.Features.length} itens · ${status}`);
}
print(EJSON.stringify(col.findOne({ Version: versions[0] }), null, 2));

quit(0);
