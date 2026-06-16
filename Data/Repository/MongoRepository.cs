using Microsoft.Extensions.Options;
using MongoDB.Driver;
using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Data.Repository;

public class MongoRepository<T> : IMongoRepository<T> where T : class
{
    private readonly IMongoCollection<T> _collection;

    public IMongoCollection<T> Collection => _collection;

    public MongoRepository(IOptions<MongoOptions> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<T>(typeof(T).Name);
    }

    public async Task<List<T>> GetAllAsync()
        => await _collection.Find(_ => true).ToListAsync();

    public async Task CreateAsync(T entity)
        => await _collection.InsertOneAsync(entity);
}
