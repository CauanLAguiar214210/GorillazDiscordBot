using System.Collections.Concurrent;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class GuildInteractionRepository : MongoRepository<GuildInteraction>, IGuildInteractionRepository
{
    private static readonly (string Trigger, string Response)[] DefaultInteractions =
    {
        ("banana", "Cadê?! Cadê?! Cadê?!")
    };

    private readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, GuildInteraction>> _cache = new();
    private readonly ConcurrentDictionary<ulong, bool> _seeded = new();
    private readonly ILogger<GuildInteractionRepository> _logger;

    public GuildInteractionRepository(
        IOptions<MongoOptions> options,
        ILogger<GuildInteractionRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    public async Task<GuildInteraction?> GetAsync(ulong guildId, string trigger)
    {
        var guildCache = await GetGuildCacheAsync(guildId);
        return guildCache.TryGetValue(trigger, out var interaction) ? interaction : null;
    }

    public async Task<List<GuildInteraction>> GetAllAsync(ulong guildId)
    {
        var guildCache = await GetGuildCacheAsync(guildId);
        return guildCache.Values.ToList();
    }

    public async Task<bool> AddAsync(GuildInteraction interaction)
    {
        var guildCache = await GetGuildCacheAsync(interaction.GuildId);

        if (!guildCache.TryAdd(interaction.Trigger, interaction))
            return false;

        try
        {
            await CreateAsync(interaction);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir interação '{trigger}' do servidor {guildId}",
                interaction.Trigger, interaction.GuildId);
        }

        return true;
    }

    public async Task<bool> RemoveAsync(ulong guildId, string trigger)
    {
        var guildCache = await GetGuildCacheAsync(guildId);

        if (!guildCache.TryRemove(trigger, out _))
            return false;

        try
        {
            var filter = Builders<GuildInteraction>.Filter.Eq(i => i.GuildId, guildId)
                & Builders<GuildInteraction>.Filter.Eq(i => i.Trigger, trigger);
            await Collection.DeleteOneAsync(filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover interação '{trigger}' do servidor {guildId}",
                trigger, guildId);
        }

        return true;
    }

    private async Task<ConcurrentDictionary<string, GuildInteraction>> GetGuildCacheAsync(ulong guildId)
    {
        if (_cache.TryGetValue(guildId, out var existing))
            return existing;

        var dict = new ConcurrentDictionary<string, GuildInteraction>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var filter = Builders<GuildInteraction>.Filter.Eq(i => i.GuildId, guildId);
            var interactions = await Collection.Find(filter).ToListAsync();

            foreach (var interaction in interactions)
                dict[interaction.Trigger] = interaction;

            if (interactions.Count == 0 && !_seeded.ContainsKey(guildId))
            {
                await SeedDefaultsAsync(guildId, dict);
                _seeded[guildId] = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar interações do servidor {guildId}", guildId);
        }

        _cache[guildId] = dict;
        return dict;
    }

    private async Task SeedDefaultsAsync(ulong guildId, ConcurrentDictionary<string, GuildInteraction> dict)
    {
        foreach (var (trigger, response) in DefaultInteractions)
        {
            var interaction = new GuildInteraction
            {
                GuildId = guildId,
                Trigger = trigger,
                Response = response,
                AddedBy = 0,
                CreatedAt = DateTime.UtcNow
            };

            dict[trigger] = interaction;

            try
            {
                await CreateAsync(interaction);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao semear interação padrão '{trigger}' no servidor {guildId}",
                    trigger, guildId);
            }
        }
    }
}
