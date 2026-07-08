using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class UserRepository : MongoRepository<DiscordUserProfile>, IUserRepository
{
    public UserRepository(IOptions<MongoOptions> options) : base(options) { }

    public async Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        var user = await Collection.Find(filter).FirstOrDefaultAsync();

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
            Username = username,
            Points = 0
        };

        await CreateAsync(newUser);
        return newUser;
    }

    public async Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId)
    {
        var todayStart = DateTime.UtcNow.Date;
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId)
            & (Builders<DiscordUserProfile>.Filter.Eq(u => u.LastDailyClaim, null)
               | Builders<DiscordUserProfile>.Filter.Lt(u => u.LastDailyClaim, todayStart));

        var update = Builders<DiscordUserProfile>.Update
            .Inc(u => u.Points, 100)
            .Set(u => u.LastDailyClaim, DateTime.UtcNow);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<DiscordUserProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0);
        return (true, result.Points);
    }

    public async Task<bool> AddPointsAsync(ulong userId, int amount)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        var update = Builders<DiscordUserProfile>.Update.Inc(u => u.Points, amount);
        var result = await Collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<List<DiscordUserProfile>> GetTopUsersAsync(int limit)
    {
        return await Collection.Find(_ => true)
            .SortByDescending(u => u.Points)
            .Limit(limit)
            .ToListAsync();
    }
}
