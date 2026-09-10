using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Entity.Ranking;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class RankingRepository : MongoRepository<RankingTier>, IRankingRepository
{
    private readonly IMongoCollection<HallOfFame> _hallOfFame;
    private readonly ILogger<RankingRepository> _logger;

    public RankingRepository(IOptions<MongoOptions> options, ILogger<RankingRepository> logger)
        : base(options)
    {
        _logger = logger;
        _hallOfFame = Collection.Database.GetCollection<HallOfFame>(nameof(HallOfFame));
    }

    public async Task<List<RankingTier>> GetTiersAsync()
    {
        var filter = Builders<RankingTier>.Filter.Eq(t => t.IsActive, true);
        return await Collection.Find(filter)
            .SortBy(t => t.SortOrder)
            .ToListAsync();
    }

    public async Task<RankingTier?> GetActiveTierForAsync(ulong netWorth)
    {
        var tiers = await GetTiersAsync();
        if (tiers.Count == 0) return null;

        RankingTier? best = null;
        foreach (var tier in tiers)
        {
            if (netWorth >= tier.MinNetWorth)
                best = tier;
            else
                break;
        }
        return best;
    }

    public async Task<List<HallOfFame>> GetHallOfFameAsync()
        => await _hallOfFame.Find(_ => true)
            .SortBy(h => h.SortOrder)
            .ToListAsync();

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _hallOfFame.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<HallOfFame>(
                    Builders<HallOfFame>.IndexKeys.Ascending(h => h.UserId),
                    new CreateIndexOptions { Name = "idx_UserId" }),
                new CreateIndexModel<HallOfFame>(
                    Builders<HallOfFame>.IndexKeys.Ascending(h => h.SortOrder),
                    new CreateIndexOptions { Name = "idx_SortOrder" })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao garantir índices da collection HallOfFame");
        }
    }
}