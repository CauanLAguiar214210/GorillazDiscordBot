using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGuildMemberRepository
{
    Task<GuildMember?> GetAsync(ulong guildId, ulong userId);
    Task<List<GuildMember>> GetAllAsync(ulong guildId);
    Task AddWarningAsync(ulong guildId, ulong userId, string username, UserWarning warning);
    Task<bool> RemoveWarningAsync(ulong guildId, ulong userId, string warningId);
    Task SetMuteAsync(ulong guildId, ulong userId, string username, DateTime? until);
    Task SetBanAsync(ulong guildId, ulong userId, string username, bool isBanned);
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}