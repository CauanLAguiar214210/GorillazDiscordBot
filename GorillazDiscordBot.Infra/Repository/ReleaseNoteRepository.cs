using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Entity.Release;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class ReleaseNoteRepository : MongoRepository<ReleaseNote>, IReleaseNoteRepository
{
    private readonly ILogger<ReleaseNoteRepository> _logger;

    public ReleaseNoteRepository(IOptions<MongoOptions> options, ILogger<ReleaseNoteRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    internal ReleaseNoteRepository(IMongoCollection<ReleaseNote> collection, ILogger<ReleaseNoteRepository> logger)
        : base(collection)
    {
        _logger = logger;
    }

    public async Task<List<ReleaseNote>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
        => await Collection.Find(_ => true)
            .SortByDescending(r => r.PublishedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public async Task<List<ReleaseNote>> GetUnannouncedAsync(DateTime utcNow, int limit, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReleaseNote>.Filter.And(
            Builders<ReleaseNote>.Filter.Eq(r => r.AnnouncedAt, null),
            Builders<ReleaseNote>.Filter.Lte(r => r.PublishedAt, utcNow));

        return await Collection.Find(filter)
            .SortBy(r => r.PublishedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReleaseNote?> GetByVersionAsync(string version, CancellationToken cancellationToken = default)
        => await Collection.Find(Builders<ReleaseNote>.Filter.Eq(r => r.Version, version))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ReleaseNote?> GetLatestAsync(CancellationToken cancellationToken = default)
        => await Collection.Find(Builders<ReleaseNote>.Filter.Lte(r => r.PublishedAt, DateTime.UtcNow))
            .SortByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryMarkAnnouncedAsync(string version, DateTime announcedAtUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ReleaseNote>.Filter.And(
            Builders<ReleaseNote>.Filter.Eq(r => r.Version, version),
            Builders<ReleaseNote>.Filter.Eq(r => r.AnnouncedAt, null));

        var update = Builders<ReleaseNote>.Update.Set(r => r.AnnouncedAt, announcedAtUtc);
        var result = await Collection.UpdateOneAsync(filter, update, null, cancellationToken);
        return result.ModifiedCount == 1;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<ReleaseNote>(
                    Builders<ReleaseNote>.IndexKeys.Ascending(r => r.Version),
                    new CreateIndexOptions { Unique = true, Name = "uq_ReleaseNote_Version" }),
                new CreateIndexModel<ReleaseNote>(
                    Builders<ReleaseNote>.IndexKeys.Ascending(r => r.PublishedAt),
                    new CreateIndexOptions { Name = "idx_ReleaseNote_PublishedAt" })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao garantir índices da collection ReleaseNote");
        }
    }
}
