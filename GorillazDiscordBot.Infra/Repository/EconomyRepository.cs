using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class EconomyRepository : IEconomyRepository
{
    private readonly IMongoCollection<EconomyProfile> _collection;
    private readonly IMongoCollection<EconomyTransaction> _transactions;

    public EconomyRepository(IOptions<MongoOptions> options)
    {
        MongoMappings.Register();
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<EconomyProfile>(nameof(EconomyProfile));
        _transactions = database.GetCollection<EconomyTransaction>(nameof(EconomyTransaction));
    }

    public async Task<EconomyProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var profile = await _collection.Find(filter).FirstOrDefaultAsync();

        if (profile != null)
        {
            if (profile.Username != username)
            {
                var update = Builders<EconomyProfile>.Update.Set(p => p.Username, username);
                await _collection.UpdateOneAsync(filter, update);
            }
            return profile;
        }

        var newProfile = new EconomyProfile { UserId = userId, Username = username };
        await _collection.InsertOneAsync(newProfile);
        return newProfile;
    }

    public async Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId, int reward)
    {
        var todayStart = DateTime.UtcNow.Date;
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & (Builders<EconomyProfile>.Filter.Eq(p => p.LastDailyClaim, null)
               | Builders<EconomyProfile>.Filter.Lt(p => p.LastDailyClaim, todayStart));

        var update = Builders<EconomyProfile>.Update
            .Inc(p => p.Money, reward)
            .Set(p => p.LastDailyClaim, DateTime.UtcNow);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.Daily, reward, "Daily resgatado");
        return (true, result.Money);
    }

    public async Task<bool> AddMoneyAsync(ulong userId, int amount, EconomyTransactionType type, string description)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update.Inc(p => p.Money, amount);
        var result = await _collection.UpdateOneAsync(filter, update);

        if (result.ModifiedCount == 0) return false;

        await AddTransactionAsync(userId, type, amount, description);
        return true;
    }

    public async Task<(bool success, int newBalance)> TryDeductMoneyAsync(ulong userId, int amount, EconomyTransactionType type, string description)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = Builders<EconomyProfile>.Update.Inc(p => p.Money, -amount);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0);

        await AddTransactionAsync(userId, type, -amount, description);
        return (true, result.Money);
    }

    public async Task<(bool success, int wallet, int bank)> DepositAsync(ulong userId, int amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = Builders<EconomyProfile>.Update
            .Inc(p => p.Money, -amount)
            .Inc(p => p.Bank, amount);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.Deposit, -amount, "Depósito no banco");
        return (true, result.Money, result.Bank);
    }

    public async Task<(bool success, int wallet, int bank)> WithdrawAsync(ulong userId, int amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Bank, amount);

        var update = Builders<EconomyProfile>.Update
            .Inc(p => p.Bank, -amount)
            .Inc(p => p.Money, amount);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.Withdraw, amount, "Saque do banco");
        return (true, result.Money, result.Bank);
    }

    public async Task<(bool success, int wallet, int savings, int streak)> DepositSavingsAsync(ulong userId, int amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = Builders<EconomyProfile>.Update
            .Inc(p => p.Money, -amount)
            .Inc(p => p.Savings, amount)
            .Inc(p => p.SavingsStreak, 1);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.SavingsDeposit, -amount, "Depósito na poupança");
        return (true, result.Money, result.Savings, result.SavingsStreak);
    }

    public async Task<(bool success, int wallet, int savings, int streak)> WithdrawSavingsAsync(ulong userId, int amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Savings, amount);

        var update = Builders<EconomyProfile>.Update
            .Inc(p => p.Savings, -amount)
            .Inc(p => p.Money, amount)
            .Set(p => p.SavingsStreak, 0);

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.SavingsWithdraw, amount, "Resgate da poupança");
        return (true, result.Money, result.Savings, result.SavingsStreak);
    }

    public async Task<EconomyProfile> SetLastWorkAsync(ulong userId, DateTime now)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update.Set(p => p.LastWorkTime, now);
        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });
        return result!;
    }

    public async Task<EconomyProfile> SetRobAttemptAsync(ulong userId, DateTime attemptTime, DateTime? caughtUntil)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update
            .Set(p => p.LastRobTime, attemptTime)
            .Set(p => p.RobCaughtUntil, caughtUntil);
        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });
        return result!;
    }

    public async Task<List<EconomyProfile>> GetTopUsersAsync(int limit)
    {
        return await _collection.Find(_ => true)
            .SortByDescending(p => p.Money)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<int> ApplyDailyMaintenanceAsync()
    {
        var today = DateTime.UtcNow.Date;

        var bankFilter = Builders<EconomyProfile>.Filter.Gt(p => p.Bank, 1);
        var savingsFilter = Builders<EconomyProfile>.Filter.And(
            Builders<EconomyProfile>.Filter.Gt(p => p.Savings, 0),
            Builders<EconomyProfile>.Filter.Or(
                Builders<EconomyProfile>.Filter.Eq(p => p.SavingsLastInterestDate, null),
                Builders<EconomyProfile>.Filter.Lt(p => p.SavingsLastInterestDate, today)));

        var filter = Builders<EconomyProfile>.Filter.Or(bankFilter, savingsFilter);
        var profiles = await _collection.Find(filter).ToListAsync();

        if (profiles.Count == 0) return 0;

        var bulkOps = new List<WriteModel<EconomyProfile>>();
        var toLog = new List<EconomyTransaction>();

        foreach (var profile in profiles)
        {
            var ops = new List<UpdateDefinition<EconomyProfile>>();

            if (profile.Bank > 1)
            {
                var newBank = (int)Math.Ceiling(profile.Bank * 0.99);
                if (newBank != profile.Bank)
                {
                    ops.Add(Builders<EconomyProfile>.Update.Set(p => p.Bank, newBank));
                    toLog.Add(MakeTransaction(profile.UserId, EconomyTransactionType.Tax, newBank - profile.Bank, "Taxa bancária diária"));
                }
            }

            if (profile.Savings > 0 && (profile.SavingsLastInterestDate is null || profile.SavingsLastInterestDate.Value < today))
            {
                var rate = EconomyRules.GetDailyInterestRate(Random.Shared, profile.SavingsStreak);
                var interest = EconomyRules.ComputeInterestAmount(profile.Savings, rate);
                if (interest > 0)
                {
                    ops.Add(Builders<EconomyProfile>.Update.Inc(p => p.Savings, interest));
                    ops.Add(Builders<EconomyProfile>.Update.Set(p => p.SavingsLastInterestDate, DateTime.UtcNow));
                    toLog.Add(MakeTransaction(profile.UserId, EconomyTransactionType.Interest, interest,
                        $"Juros da poupança ({rate:P1} ao dia)"));
                }
            }

            if (ops.Count == 0) continue;

            var update = Builders<EconomyProfile>.Update.Combine(ops.ToArray());
            var userFilter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, profile.UserId);
            bulkOps.Add(new UpdateOneModel<EconomyProfile>(userFilter, update));
        }

        if (bulkOps.Count == 0) return 0;

        await _collection.BulkWriteAsync(bulkOps);
        if (toLog.Count > 0)
            await _transactions.InsertManyAsync(toLog);

        return bulkOps.Count;
    }

    public async Task<List<EconomyTransaction>> GetHistoryAsync(ulong userId, int limit)
    {
        var filter = Builders<EconomyTransaction>.Filter.Eq(t => t.UserId, userId);
        return await _transactions.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Limit(Math.Clamp(limit, 1, 50))
            .ToListAsync();
    }

    private async Task AddTransactionAsync(ulong userId, EconomyTransactionType type, int amount, string description)
    {
        await _transactions.InsertOneAsync(new EconomyTransaction
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static EconomyTransaction MakeTransaction(ulong userId, EconomyTransactionType type, int amount, string description)
        => new()
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
}