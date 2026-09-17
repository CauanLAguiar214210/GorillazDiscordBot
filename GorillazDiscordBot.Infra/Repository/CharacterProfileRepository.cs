using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class CharacterProfileRepository : MongoRepository<CharacterProfile>, ICharacterProfileRepository
{
    private readonly ILogger<CharacterProfileRepository> _logger;

    public CharacterProfileRepository(IOptions<MongoOptions> options, ILogger<CharacterProfileRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    internal CharacterProfileRepository(IMongoCollection<CharacterProfile> collection, ILogger<CharacterProfileRepository> logger)
        : base(collection)
    {
        _logger = logger;
    }

    public async Task<CharacterProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<CharacterProfile>.Filter.Eq(p => p.UserId, userId);
        var profile = await Collection.Find(filter).FirstOrDefaultAsync();

        if (profile != null)
        {
            var needsUsernameUpdate = profile.Username != username;
            if (needsUsernameUpdate)
            {
                var update = Builders<CharacterProfile>.Update
                    .Set(p => p.Username, username)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow);
                await Collection.UpdateOneAsync(filter, update);
                profile.Username = username;
            }
            return profile;
        }

        profile = new CharacterProfile
        {
            UserId = userId,
            Username = username,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await Collection.InsertOneAsync(profile);
        return profile;
    }

    public async Task<CharacterProfile?> GetAsync(ulong userId)
    {
        var filter = Builders<CharacterProfile>.Filter.Eq(p => p.UserId, userId);
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task SaveAsync(CharacterProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<CharacterProfile>.Filter.Eq(p => p.UserId, profile.UserId);
        var options = new ReplaceOptions { IsUpsert = true };
        await Collection.ReplaceOneAsync(filter, profile, options);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<CharacterProfile>(
                    Builders<CharacterProfile>.IndexKeys.Ascending(p => p.UserId),
                    new CreateIndexOptions { Unique = true, Name = "uq_Profile_UserId" })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao garantir índices da collection CharacterProfile");
        }
    }
}