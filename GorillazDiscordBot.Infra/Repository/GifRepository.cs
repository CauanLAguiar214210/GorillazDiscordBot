using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class GifRepository : MongoRepository<Gif>, IGifRepository
{
    public GifRepository(IOptions<MongoOptions> options) : base(options) { }

    public async Task<Gif?> GetByNomeAsync(ulong guildId, string nome)
    {
        var nomeFilter = Builders<Gif>.Filter.Regex(g => g.Nome, new BsonRegularExpression($"^{nome}$", "i"));

        var gif = await Collection
            .Find(nomeFilter & Builders<Gif>.Filter.Eq(g => g.GuildId, guildId))
            .FirstOrDefaultAsync();

        if (gif != null) return gif;

        return await Collection
            .Find(nomeFilter & Builders<Gif>.Filter.Eq(g => g.GuildId, 0UL))
            .FirstOrDefaultAsync();
    }

    public async Task<Gif?> GetRandomAsync(ulong guildId)
    {
        var filter = VisibleFilter(guildId);
        var count = await Collection.CountDocumentsAsync(filter);
        if (count == 0) return null;

        var randomIndex = new Random().Next(0, (int)count);
        return await Collection.Find(filter).Skip(randomIndex).Limit(1).FirstOrDefaultAsync();
    }

    public async Task<List<Gif>> GetAllAsync(ulong guildId)
        => await Collection.Find(VisibleFilter(guildId)).ToListAsync();

    public async Task<bool> RemoveAsync(ulong guildId, string nome)
    {
        var filter = Builders<Gif>.Filter.Eq(g => g.GuildId, guildId) &
                     Builders<Gif>.Filter.Regex(g => g.Nome, new BsonRegularExpression($"^{nome}$", "i"));

        var result = await Collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    private static FilterDefinition<Gif> VisibleFilter(ulong guildId)
        => Builders<Gif>.Filter.Or(
            Builders<Gif>.Filter.Eq(g => g.GuildId, guildId),
            Builders<Gif>.Filter.Eq(g => g.GuildId, 0UL));
}
