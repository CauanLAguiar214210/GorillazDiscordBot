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
            Money = 0,
            Bank = 0
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
            .Inc(u => u.Money, 100)
            .Set(u => u.LastDailyClaim, DateTime.UtcNow);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<DiscordUserProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0);
        return (true, result.Money);
    }

    public async Task<bool> AddMoneyAsync(ulong userId, int amount)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        var update = Builders<DiscordUserProfile>.Update.Inc(u => u.Money, amount);
        var result = await Collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<(bool success, int wallet, int bank)> DepositAsync(ulong userId, int amount)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId)
            & Builders<DiscordUserProfile>.Filter.Gte(u => u.Money, amount);

        var update = Builders<DiscordUserProfile>.Update
            .Inc(u => u.Money, -amount)
            .Inc(u => u.Bank, amount);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<DiscordUserProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);
        return (true, result.Money, result.Bank);
    }

    public async Task<(bool success, int wallet, int bank)> WithdrawAsync(ulong userId, int amount)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId)
            & Builders<DiscordUserProfile>.Filter.Gte(u => u.Bank, amount);

        var update = Builders<DiscordUserProfile>.Update
            .Inc(u => u.Bank, -amount)
            .Inc(u => u.Money, amount);

        var result = await Collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<DiscordUserProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);
        return (true, result.Money, result.Bank);
    }

    public async Task<List<DiscordUserProfile>> GetTopUsersAsync(int limit)
    {
        return await Collection.Find(_ => true)
            .SortByDescending(u => u.Money)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<int> ApplyBankTaxAsync()
    {
        var filter = Builders<DiscordUserProfile>.Filter.Gt(u => u.Bank, 1);
        var users = await Collection.Find(filter).ToListAsync();

        if (users.Count == 0) return 0;

        var bulkOps = new List<WriteModel<DiscordUserProfile>>();

        foreach (var user in users)
        {
            var newBank = (int)Math.Ceiling(user.Bank * 0.99);
            if (newBank == user.Bank) continue;

            var updateFilter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, user.UserId)
                & Builders<DiscordUserProfile>.Filter.Eq(u => u.Bank, user.Bank);

            var update = Builders<DiscordUserProfile>.Update.Set(u => u.Bank, newBank);
            bulkOps.Add(new UpdateOneModel<DiscordUserProfile>(updateFilter, update));
        }

        if (bulkOps.Count == 0) return 0;

        var result = await Collection.BulkWriteAsync(bulkOps);
        return (int)result.ModifiedCount;
    }
}
