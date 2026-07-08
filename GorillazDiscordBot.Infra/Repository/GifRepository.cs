using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class GifRepository : MongoRepository<Gif>, IGifRepository
{
    public GifRepository(IOptions<MongoOptions> options) : base(options) { }

    public async Task<Gif?> GetByNomeAsync(string nome)
    {
        var filter = Builders<Gif>.Filter.Regex(g => g.Nome, new MongoDB.Bson.BsonRegularExpression($"^{nome}$", "i"));
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<Gif?> GetRandomAsync()
    {
        var count = await Collection.CountDocumentsAsync(_ => true);
        if (count == 0) return null;

        var randomIndex = new Random().Next(0, (int)count);
        return await Collection.Find(_ => true).Skip(randomIndex).Limit(1).FirstOrDefaultAsync();
    }
}
