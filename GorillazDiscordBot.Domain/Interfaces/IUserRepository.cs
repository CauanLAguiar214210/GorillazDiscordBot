using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IUserRepository
{
    Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username);
    Task<DiscordUserProfile?> GetAsync(ulong userId);
    Task<ulong> GetMainIdAsync(ulong userId);
    Task<List<DiscordUserProfile>> GetGroupAsync(ulong userId);
    Task<bool> LinkAsync(ulong mainId, ulong altId);
    Task<bool> UnlinkAsync(ulong accountId);
    Task<List<DiscordUserProfile>> GetAllAsync();
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}