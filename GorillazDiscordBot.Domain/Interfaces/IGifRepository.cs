using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGifRepository
{
    Task<Gif?> GetByNomeAsync(string nome);
    Task<Gif?> GetRandomAsync();
    Task<List<Gif>> GetAllAsync();
    Task<List<Gif>> GetPaginatedAsync(int page, int pageSize, string? categoria = null);
    Task<int> GetCountAsync(string? categoria = null);
    Task<List<string>> GetCategoriasAsync();
    Task CreateAsync(Gif entity);
    Task<bool> DeleteByNomeAsync(string nome);
}
