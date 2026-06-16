using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public interface IMongoRepository<T> where T : class
{
    IMongoCollection<T> Collection { get; }
    Task<List<T>> GetAllAsync();
    Task CreateAsync(T entity);
}
