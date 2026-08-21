using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface ISoundInteractionRepository
{
    Task<GuildSoundInteraction?> GetAsync(ulong guildId, string trigger);
    Task<List<GuildSoundInteraction>> GetAllAsync(ulong guildId);
    Task<bool> AddAsync(GuildSoundInteraction sound);
    Task<bool> RemoveAsync(ulong guildId, string trigger);
}
