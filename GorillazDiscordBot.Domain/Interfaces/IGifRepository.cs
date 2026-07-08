using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGifRepository
{
    Task<Gif?> GetByNomeAsync(string nome);
    Task<Gif?> GetRandomAsync();
    Task<List<Gif>> GetAllAsync();
    Task CreateAsync(Gif entity);
}
