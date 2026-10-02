// seed-midnight-monkey.js
// ============================================================================
// Cria o agendamento diário do oleodemacaco para todas as guildas cadastradas.
// O canal 0 significa "primeiro canal de voz ocupado" e é resolvido pelo bot.
//
// Rodar:
//   mongosh "mongodb://localhost:27017/gorillazbot" scripts/mongodb/seed-midnight-monkey.js
//   docker exec -i gorillaz-mongodb mongosh --db gorillazbot < scripts/mongodb/seed-midnight-monkey.js
//
// Idempotente: guildas que já possuem este agendamento não recebem outro.
// O horário é meia-noite de Brasília. Em UTC, 00:00 BRT = 03:00.
// ============================================================================

const collection = db.getCollection("Guild");
const source = "local:oleodemacaco.mp3";
const schedule = {
    Id: UUID(),
    AudioSource: source,
    Day: null,
    // MongoDB's default TimeSpanSerializer stores ticks (100 ns).
    Time: NumberLong("108000000000"),
    VoiceChannelId: NumberLong(0),
    Enabled: true,
    TimesPlayed: 0
};

const guilds = collection.find({ GuildId: { $exists: true } }, { GuildId: 1 }).toArray();
const operations = [];

for (const guild of guilds) {
    const alreadyScheduled = collection.findOne({
        _id: guild._id,
        "ScheduledSounds.AudioSource": source,
        "ScheduledSounds.Time": schedule.Time,
        "ScheduledSounds.Day": null
    });

    if (!alreadyScheduled) {
        operations.push({
            updateOne: {
                filter: { _id: guild._id },
                update: { $push: { ScheduledSounds: schedule } }
            }
        });
    }
}

if (operations.length > 0) {
    // ScheduledSounds is an embedded array, so bulkWrite is the safe equivalent
    // of insertMany: it updates each existing Guild without replacing settings.
    const result = collection.bulkWrite(operations, { ordered: false });
    print(`bulkWrite: matched=${result.matchedCount}, modified=${result.modifiedCount}`);
} else {
    print("Nenhum agendamento novo: todas as guildas já possuem oleodemacaco à meia-noite.");
}

print(`Guildas avaliadas: ${guilds.length}; operações: ${operations.length}`);
quit(0);
