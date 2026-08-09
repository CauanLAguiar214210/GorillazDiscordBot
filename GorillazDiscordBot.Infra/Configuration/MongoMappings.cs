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
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.Username).SetElementName("Username");
            map.MapMember(c => c.Points).SetElementName("Points");
            map.MapMember(c => c.LastDailyClaim).SetElementName("LastDailyClaim");
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

        BsonClassMap.RegisterClassMap<Joke>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.Texto).SetElementName("text");
            map.MapMember(c => c.Categoria).SetElementName("category");
        });

        RegisterGuildSettings<GuildWelcomeSettings>();
        RegisterGuildSettings<GuildVoiceSettings>();
    }

    private static void RegisterGuildSettings<T>()
    {
        var classMap = new BsonClassMap(typeof(T));
        classMap.AutoMap();
        classMap.SetIdMember(classMap.GetMemberMap(nameof(IGuildSettings.GuildId)));
        BsonClassMap.RegisterClassMap(classMap);
    }
}
