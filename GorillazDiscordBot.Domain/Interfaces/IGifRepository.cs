using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGifRepository
{
    Task<Gif?> GetByNomeAsync(ulong guildId, string nome);
    Task<Gif?> GetRandomAsync(ulong guildId);
    Task<List<Gif>> GetAllAsync(ulong guildId);
    Task<bool> RemoveAsync(ulong guildId, string nome);
    Task CreateAsync(Gif entity);
}
