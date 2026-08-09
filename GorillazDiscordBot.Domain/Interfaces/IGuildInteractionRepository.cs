using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGuildInteractionRepository
{
    Task<GuildInteraction?> GetAsync(ulong guildId, string trigger);
    Task<List<GuildInteraction>> GetAllAsync(ulong guildId);
    Task<bool> AddAsync(GuildInteraction interaction);
    Task<bool> RemoveAsync(ulong guildId, string trigger);
}
