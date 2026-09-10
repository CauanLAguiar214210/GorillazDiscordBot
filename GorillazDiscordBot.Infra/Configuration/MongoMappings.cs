using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Infra.Configuration;

public static class MongoMappings
{
    private static readonly object RegisterLock = new();
    private static bool _registered;

    public static void Register()
    {
        lock (RegisterLock)
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
            map.MapMember(c => c.MainUserId).SetElementName("MainUserId");
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
            map.MapMember(c => c.DailyBoostPending).SetElementName("DailyBoostPending");
            map.MapMember(c => c.WorkBoostPending).SetElementName("WorkBoostPending");
            map.MapMember(c => c.RobShieldUntil).SetElementName("RobShieldUntil");
            map.MapMember(c => c.DailyBoostExpiresAt).SetElementName("DailyBoostExpiresAt");
            map.MapMember(c => c.WorkBoostExpiresAt).SetElementName("WorkBoostExpiresAt");
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

        BsonClassMap.RegisterClassMap<GuildMember>(map =>
        {
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.GuildId).SetElementName("GuildId");
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.Username).SetElementName("Username");
            map.MapMember(c => c.Warnings).SetElementName("Warnings");
            map.MapMember(c => c.MuteUntil).SetElementName("MuteUntil");
            map.MapMember(c => c.IsBanned).SetElementName("IsBanned");
            map.MapMember(c => c.CreatedAt).SetElementName("CreatedAt");
            map.MapMember(c => c.UpdatedAt).SetElementName("UpdatedAt");
        });

        RegisterGuildSettings<Guild>();

        BsonClassMap.RegisterClassMap<InventoryItem>(map =>
        {
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.UserId).SetElementName("UserId");
            map.MapMember(c => c.ItemKey).SetElementName("ItemKey");
            map.MapMember(c => c.Quantity).SetElementName("Quantity");
            map.MapMember(c => c.ExpiresAt).SetElementName("ExpiresAt");
            map.MapMember(c => c.AcquiredAt).SetElementName("AcquiredAt");
            map.MapMember(c => c.LastCollectedAt).SetElementName("LastCollectedAt");
            map.MapMember(c => c.IsEquipped).SetElementName("IsEquipped");
            map.MapMember(c => c.PetNickname).SetElementName("PetNickname");
        });

        BsonClassMap.RegisterClassMap<ShopItem>(map =>
        {
            map.SetIgnoreExtraElements(true);
            map.MapIdMember(c => c.Id)
               .SetSerializer(new StringSerializer(BsonType.ObjectId))
               .SetIdGenerator(StringObjectIdGenerator.Instance);
            map.MapMember(c => c.Key).SetElementName("Key");
            map.MapMember(c => c.Name).SetElementName("Name");
            map.MapMember(c => c.Emoji).SetElementName("Emoji");
            map.MapMember(c => c.Description).SetElementName("Description");
            map.MapMember(c => c.Price).SetElementName("Price");
            map.MapMember(c => c.Category).SetElementName("Category");
            map.MapMember(c => c.Effect).SetElementName("Effect");
            map.MapMember(c => c.DurationHours).SetElementName("DurationHours");
            map.MapMember(c => c.DailyIncome).SetElementName("DailyIncome");
            map.MapMember(c => c.MaxQuantity).SetElementName("MaxQuantity");
            map.MapMember(c => c.IsActive).SetElementName("IsActive");
            map.MapMember(c => c.IsPlaceholder).SetElementName("IsPlaceholder");
            map.MapMember(c => c.SortOrder).SetElementName("SortOrder");
            map.MapMember(c => c.RelicEffect).SetElementName("RelicEffect");
            map.MapMember(c => c.RelicGame).SetElementName("RelicGame");
            map.MapMember(c => c.RelicValue).SetElementName("RelicValue");
            map.MapMember(c => c.UpgradeEffect).SetElementName("UpgradeEffect");
            map.MapMember(c => c.UpgradeValue).SetElementName("UpgradeValue");
            map.MapMember(c => c.TargetPetKey).SetElementName("TargetPetKey");
            map.MapMember(c => c.EvolvedName).SetElementName("EvolvedName");
            map.MapMember(c => c.EvolvedEmoji).SetElementName("EvolvedEmoji");
            map.MapMember(c => c.EvolvedUpgradeValue).SetElementName("EvolvedUpgradeValue");
        });
        }
    }

    private static void RegisterGuildSettings<T>()
    {
        var classMap = new BsonClassMap(typeof(T));
        classMap.AutoMap();
        classMap.SetIdMember(classMap.GetMemberMap(nameof(IGuildSettings.GuildId)));
        BsonClassMap.RegisterClassMap(classMap);
    }
}
