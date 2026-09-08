using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class UserRepository : MongoRepository<DiscordUserProfile>, IUserRepository
{
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IOptions<MongoOptions> options, ILogger<UserRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    internal UserRepository(IMongoCollection<DiscordUserProfile> collection, ILogger<UserRepository> logger)
        : base(collection)
    {
        _logger = logger;
    }

    public async Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        var user = await FirstOrDefaultAsync(filter);

        if (user != null)
        {
            if (user.Username != username)
            {
                var update = Builders<DiscordUserProfile>.Update.Set(u => u.Username, username);
                await Collection.UpdateOneAsync(filter, update);
            }
            return user;
        }

        var newUser = new DiscordUserProfile
        {
            UserId = userId,
            Username = username
        };

        await CreateAsync(newUser);
        return newUser;
    }

    public async Task<DiscordUserProfile?> GetAsync(ulong userId)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        return await FirstOrDefaultAsync(filter);
    }

    private async Task<DiscordUserProfile?> FirstOrDefaultAsync(FilterDefinition<DiscordUserProfile> filter)
    {
        var cursor = await Collection.FindAsync(filter, null, CancellationToken.None);
        return await cursor.FirstOrDefaultAsync(CancellationToken.None);
    }

    public async Task<ulong> GetMainIdAsync(ulong userId)
    {
        var profile = await GetAsync(userId);
        if (profile == null || profile.MainUserId == 0)
            return userId;

        return profile.MainUserId;
    }

    public async Task<List<DiscordUserProfile>> GetGroupAsync(ulong userId)
    {
        var mainId = await GetMainIdAsync(userId);
        var members = new List<DiscordUserProfile>();

        var mainProfile = await GetAsync(mainId);
        if (mainProfile != null)
        {
            members.Add(mainProfile);
        }
        else
        {
            members.Add(new DiscordUserProfile { UserId = mainId });
        }

        var altsCursor = await Collection.FindAsync(
            Builders<DiscordUserProfile>.Filter.Eq(u => u.MainUserId, mainId),
            null,
            CancellationToken.None);
        var alts = await altsCursor.ToListAsync(CancellationToken.None);

        members.AddRange(alts.OrderBy(a => a.UserId));
        return members;
    }

    public async Task<bool> LinkAsync(ulong mainId, ulong altId)
    {
        var alt = await GetAsync(altId);

        if (alt != null)
        {
            var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, altId);
            var update = Builders<DiscordUserProfile>.Update.Set(u => u.MainUserId, mainId);
            var result = await Collection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        await CreateAsync(new DiscordUserProfile
        {
            UserId = altId,
            MainUserId = mainId
        });
        return true;
    }

    public async Task<bool> UnlinkAsync(ulong accountId)
    {
        var profile = await GetAsync(accountId);
        if (profile == null || profile.MainUserId == 0)
            return false;

        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, accountId);
        var update = Builders<DiscordUserProfile>.Update.Set(u => u.MainUserId, 0UL);
        var result = await Collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<DiscordUserProfile>(
                    Builders<DiscordUserProfile>.IndexKeys.Ascending(u => u.UserId),
                    new CreateIndexOptions { Unique = true, Name = "uq_UserId" }),
                new CreateIndexModel<DiscordUserProfile>(
                    Builders<DiscordUserProfile>.IndexKeys.Ascending(u => u.MainUserId),
                    new CreateIndexOptions { Name = "idx_MainUserId" })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao garantir índices da collection DiscordUserProfile");
        }
    }
}