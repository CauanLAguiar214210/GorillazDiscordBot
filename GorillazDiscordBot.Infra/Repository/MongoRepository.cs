using Microsoft.Extensions.Options;
using MongoDB.Driver;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Infra.Configuration;

namespace GorillazDiscordBot.Data.Repository;

public class MongoRepository<T> : IMongoRepository<T> where T : class
{
    public IMongoCollection<T> Collection { get; }

    public MongoRepository(IOptions<MongoOptions> options)
        : this(CreateCollection(options))
    {
    }

    protected MongoRepository(IMongoCollection<T> collection)
    {
        Collection = collection;
    }

    private static IMongoCollection<T> CreateCollection(IOptions<MongoOptions> options)
    {
        MongoMappings.Register();
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        return database.GetCollection<T>(typeof(T).Name);
    }

    public async Task<List<T>> GetAllAsync()
        => await Collection.Find(_ => true).ToListAsync();

    public async Task CreateAsync(T entity)
        => await Collection.InsertOneAsync(entity);
}
