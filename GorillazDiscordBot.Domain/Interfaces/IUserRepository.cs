using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IUserRepository
{
    Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username);
}