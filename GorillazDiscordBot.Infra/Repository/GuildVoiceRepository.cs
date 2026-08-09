using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Infra.Repository;

public class GuildVoiceRepository : IGuildVoiceRepository
{
    private readonly ConcurrentDictionary<ulong, GuildVoiceSettings> _settings = new();

    public GuildVoiceSettings Get(ulong guildId)
        => _settings.GetOrAdd(guildId, id => new GuildVoiceSettings { GuildId = id });

    public void Save(GuildVoiceSettings settings)
        => _settings[settings.GuildId] = settings;

    public void Reset(ulong guildId)
        => _settings.TryRemove(guildId, out _);
}
