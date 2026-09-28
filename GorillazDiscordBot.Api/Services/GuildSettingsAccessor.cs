using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

public class GuildSettingsAccessor
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public GuildSettingsAccessor(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    public async Task<Guild> GetAsync(ulong guildId)
        => await _guildRepository.GetAsync(guildId);

    public async Task SaveAsync(ulong guildId, Action<Guild> mutate)
    {
        var guild = await _guildRepository.GetAsync(guildId);
        mutate(guild);
        await _guildRepository.SaveAsync(guild);
    }
}