using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IUserRepository
{
    Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username);
    Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId);
    Task<bool> AddPointsAsync(ulong userId, int amount);
    Task<List<DiscordUserProfile>> GetTopUsersAsync(int limit);
}
