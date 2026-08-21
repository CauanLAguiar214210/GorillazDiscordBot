using System.Collections.Concurrent;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class GuildSoundInteractionRepository : MongoRepository<GuildSoundInteraction>, ISoundInteractionRepository
{
    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, GuildSoundInteraction>> _cache = new();
    private readonly ILogger<GuildSoundInteractionRepository> _logger;

    public GuildSoundInteractionRepository(
        IOptions<MongoOptions> options,
        ILogger<GuildSoundInteractionRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    public async Task<GuildSoundInteraction?> GetAsync(ulong guildId, string trigger)
    {
        var guildCache = await GetGuildCacheAsync(guildId);
        return guildCache.TryGetValue(trigger, out var sound) ? sound : null;
    }

    public async Task<List<GuildSoundInteraction>> GetAllAsync(ulong guildId)
    {
        var guildCache = await GetGuildCacheAsync(guildId);
        return guildCache.Values.ToList();
    }

    public async Task<bool> AddAsync(GuildSoundInteraction sound)
    {
        var guildCache = await GetGuildCacheAsync(sound.GuildId);

        if (!guildCache.TryAdd(sound.Trigger, sound))
            return false;

        try
        {
            await CreateAsync(sound);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir som '{trigger}' do servidor {guildId}",
                sound.Trigger, sound.GuildId);
        }

        return true;
    }

    public async Task<bool> RemoveAsync(ulong guildId, string trigger)
    {
        var guildCache = await GetGuildCacheAsync(guildId);

        if (!guildCache.TryRemove(trigger, out var removed))
            return false;

        try
        {
            var filter = Builders<GuildSoundInteraction>.Filter.Eq(s => s.GuildId, guildId)
                & Builders<GuildSoundInteraction>.Filter.Eq(s => s.Trigger, trigger);
            await Collection.DeleteOneAsync(filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover som '{trigger}' do servidor {guildId}",
                trigger, guildId);
        }

        return true;
    }

    private async Task<ConcurrentDictionary<string, GuildSoundInteraction>> GetGuildCacheAsync(ulong guildId)
    {
        if (_cache.TryGetValue(guildId, out var existing))
            return existing;

        var dict = new ConcurrentDictionary<string, GuildSoundInteraction>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var filter = Builders<GuildSoundInteraction>.Filter.Eq(s => s.GuildId, guildId);
            var sounds = await Collection.Find(filter).ToListAsync();

            foreach (var sound in sounds)
                dict[sound.Trigger] = sound;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar sons do servidor {guildId}", guildId);
        }

        _cache[guildId] = dict;
        return dict;
    }
}
