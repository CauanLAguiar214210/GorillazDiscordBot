using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Infra.Repository;

public class GuildWelcomeRepository : IGuildWelcomeRepository
{
    private readonly ConcurrentDictionary<ulong, GuildWelcomeSettings> _settings = new();

    public GuildWelcomeSettings Get(ulong guildId)
        => _settings.GetOrAdd(guildId, id => new GuildWelcomeSettings { GuildId = id });

    public void Save(GuildWelcomeSettings settings)
        => _settings[settings.GuildId] = settings;

    public void Reset(ulong guildId)
        => _settings.TryRemove(guildId, out _);
}
