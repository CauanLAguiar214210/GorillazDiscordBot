using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Data.Repository;

public interface IGifRepository : IMongoRepository<Gif>
{
    Task<Gif?> GetByNomeAsync(string nome);
    Task<Gif?> GetRandomAsync();
}
