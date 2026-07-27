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

    public async Task<List<Gif>> GetPaginatedAsync(int page, int pageSize, string? categoria = null)
    {
        var filter = Builders<Gif>.Filter.Empty;

        if (!string.IsNullOrEmpty(categoria))
        {
            filter = Builders<Gif>.Filter.Eq(g => g.Categoria, categoria);
        }

        var skip = (page - 1) * pageSize;

        return await Collection.Find(filter)
            .SortBy(g => g.Nome)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(string? categoria = null)
    {
        var filter = Builders<Gif>.Filter.Empty;

        if (!string.IsNullOrEmpty(categoria))
        {
            filter = Builders<Gif>.Filter.Eq(g => g.Categoria, categoria);
        }

        return (int)await Collection.CountDocumentsAsync(filter);
    }

    public async Task<List<string>> GetCategoriasAsync()
    {
        var distinct = await Collection.DistinctAsync(g => g.Categoria, Builders<Gif>.Filter.Empty);
        var categorias = new List<string>();
        while (await distinct.MoveNextAsync())
        {
            foreach (var cat in distinct.Current)
            {
                if (!string.IsNullOrEmpty(cat) && !categorias.Contains(cat))
                    categorias.Add(cat);
            }
        }
        return categorias.OrderBy(c => c).ToList();
    }

    public async Task<bool> DeleteByNomeAsync(string nome)
    {
        var filter = Builders<Gif>.Filter.Regex(g => g.Nome, new MongoDB.Bson.BsonRegularExpression($"^{nome}$", "i"));
        var result = await Collection.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }
}
