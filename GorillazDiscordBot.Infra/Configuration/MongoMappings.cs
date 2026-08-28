using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Infra.Configuration;

public static class MongoMappings
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        BsonClassMap.RegisterClassMap<DiscordUserProfile>(map =>
        {
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.Username).SetElementName("Username");
        });

        BsonClassMap.RegisterClassMap<EconomyProfile>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.Username).SetElementName("Username");
            map.MapMember(c => c.Money).SetElementName("Money");
            map.MapMember(c => c.Bank).SetElementName("Bank");
            map.MapMember(c => c.LastDailyClaim).SetElementName("LastDailyClaim");
            map.MapMember(c => c.Savings).SetElementName("Savings");
            map.MapMember(c => c.SavingsStreak).SetElementName("SavingsStreak");
            map.MapMember(c => c.SavingsLastInterestDate).SetElementName("SavingsLastInterestDate");
            map.MapMember(c => c.LastWorkTime).SetElementName("LastWorkTime");
            map.MapMember(c => c.LastRobTime).SetElementName("LastRobTime");
            map.MapMember(c => c.RobCaughtUntil).SetElementName("RobCaughtUntil");
        });

        BsonClassMap.RegisterClassMap<EconomyTransaction>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.Type).SetElementName("Type");
            map.MapMember(c => c.Amount).SetElementName("Amount");
            map.MapMember(c => c.Description).SetElementName("Description");
            map.MapMember(c => c.CreatedAt).SetElementName("CreatedAt");
        });

        BsonClassMap.RegisterClassMap<Gif>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.Nome).SetElementName("nome");
            map.MapMember(c => c.Url).SetElementName("url");
            map.MapMember(c => c.Categoria).SetElementName("categoria");
            map.MapMember(c => c.AddedBy).SetElementName("addedBy");
            map.MapMember(c => c.AddedAt).SetElementName("addedAt");
        });

        BsonClassMap.RegisterClassMap<GuildInteraction>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.GuildId).SetElementName("guildId");
            map.MapMember(c => c.Trigger).SetElementName("trigger");
            map.MapMember(c => c.Response).SetElementName("response");
            map.MapMember(c => c.AddedBy).SetElementName("addedBy");
            map.MapMember(c => c.CreatedAt).SetElementName("createdAt");
        });

        RegisterGuildSettings<GuildWelcomeSettings>();
        RegisterGuildSettings<GuildVoiceSettings>();
        RegisterGuildSettings<GuildPrefixSettings>();
    }

    private static void RegisterGuildSettings<T>()
    {
        var classMap = new BsonClassMap(typeof(T));
        classMap.AutoMap();
        classMap.SetIdMember(classMap.GetMemberMap(nameof(IGuildSettings.GuildId)));
        BsonClassMap.RegisterClassMap(classMap);
    }
}
