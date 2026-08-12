using System.Collections.Concurrent;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class SettingsRepository<T> : MongoRepository<T>, ISettingsRepository<T>
    where T : class, IGuildSettings, new()
{
    private readonly ConcurrentDictionary<ulong, T> _cache = new();
    private readonly ILogger<SettingsRepository<T>> _logger;

    public SettingsRepository(IOptions<MongoOptions> options, ILogger<SettingsRepository<T>> logger)
        : base(options)
    {
        _logger = logger;
    }

    public async Task<T> GetAsync(ulong guildId)
    {
        if (_cache.TryGetValue(guildId, out var cached))
            return cached;

        T settings;
        try
        {
            var filter = Builders<T>.Filter.Eq(s => s.GuildId, guildId);
            settings = await Collection.Find(filter).FirstOrDefaultAsync() ?? new T { GuildId = guildId };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar configurações do servidor {guildId}", guildId);
            settings = new T { GuildId = guildId };
        }

        _cache[guildId] = settings;
        return settings;
    }

    public async Task SaveAsync(T settings)
    {
        _cache[settings.GuildId] = settings;

        try
        {
            var filter = Builders<T>.Filter.Eq(s => s.GuildId, settings.GuildId);
            await Collection.ReplaceOneAsync(filter, settings, new ReplaceOptions { IsUpsert = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir configurações do servidor {guildId}", settings.GuildId);
        }
    }

    public async Task ResetAsync(ulong guildId)
    {
        _cache.TryRemove(guildId, out _);

        try
        {
            var filter = Builders<T>.Filter.Eq(s => s.GuildId, guildId);
            await Collection.DeleteOneAsync(filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover configurações do servidor {guildId}", guildId);
        }
    }
}
