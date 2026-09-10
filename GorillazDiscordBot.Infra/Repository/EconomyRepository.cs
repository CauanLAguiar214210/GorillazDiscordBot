using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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

    internal EconomyRepository(IMongoCollection<EconomyProfile> profiles, IMongoCollection<EconomyTransaction> transactions)
    {
        MongoMappings.Register();
        _collection = profiles;
        _transactions = transactions;
    }

    public async Task<EconomyProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var profile = await FirstOrDefaultAsync(filter);

        if (profile != null)
        {
            if (profile.Username != username)
            {
                var update = Builders<EconomyProfile>.Update.Set(p => p.Username, username);
                await _collection.UpdateOneAsync(filter, update);
            }
            return profile;
        }

        var newProfile = new EconomyProfile
        {
            UserId = userId,
            Username = username,
            Money = EconomyRules.WelcomeBonus
        };

        await _collection.InsertOneAsync(newProfile);
        await AddTransactionAsync(userId, EconomyTransactionType.Welcome, (long)EconomyRules.WelcomeBonus, "Bônus de boas-vindas");
        return newProfile;
    }

    private Task<EconomyProfile?> FirstOrDefaultAsync(FilterDefinition<EconomyProfile> filter)
    {
        using var cursor = _collection.FindSync(filter, null, CancellationToken.None);
        return Task.FromResult<EconomyProfile?>(
            cursor.MoveNext(CancellationToken.None) ? cursor.Current.FirstOrDefault() : null);
    }

    public async Task<(bool claimed, ulong newBalance)> TryClaimDailyAsync(ulong userId, ulong reward)
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

        await AddTransactionAsync(userId, EconomyTransactionType.Daily, (long)reward, "Daily resgatado");
        return (true, result.Money);
    }

    public async Task<bool> AddMoneyAsync(ulong userId, ulong amount, EconomyTransactionType type, string description)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update.Inc(p => p.Money, amount);
        var result = await _collection.UpdateOneAsync(filter, update);

        if (result.ModifiedCount == 0) return false;

        await AddTransactionAsync(userId, type, (long)amount, description);
        return true;
    }

    public async Task<(bool success, ulong newBalance)> TryDeductMoneyAsync(ulong userId, ulong amount, EconomyTransactionType type, string description)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = new BsonDocumentUpdateDefinition<EconomyProfile>(
            new BsonDocument("$inc", new BsonDocument("Money", -(long)amount)));

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0);

        await AddTransactionAsync(userId, type, -(long)amount, description);
        return (true, result.Money);
    }

    public async Task<(bool success, ulong wallet, ulong bank)> DepositAsync(ulong userId, ulong amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = new BsonDocumentUpdateDefinition<EconomyProfile>(
            new BsonDocument("$inc", new BsonDocument { { "Money", -(long)amount }, { "Bank", (long)amount } }));

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.Deposit, -(long)amount, "Depósito no banco");
        return (true, result.Money, result.Bank);
    }

    public async Task<(bool success, ulong wallet, ulong bank)> WithdrawAsync(ulong userId, ulong amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Bank, amount);

        var update = new BsonDocumentUpdateDefinition<EconomyProfile>(
            new BsonDocument("$inc", new BsonDocument { { "Bank", -(long)amount }, { "Money", (long)amount } }));

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.Withdraw, (long)amount, "Saque do banco");
        return (true, result.Money, result.Bank);
    }

    public async Task<(bool success, ulong wallet, ulong savings, ulong streak)> DepositSavingsAsync(ulong userId, ulong amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Money, amount);

        var update = new BsonDocumentUpdateDefinition<EconomyProfile>(
            new BsonDocument("$inc", new BsonDocument { { "Money", -(long)amount }, { "Savings", (long)amount }, { "SavingsStreak", 1 } }));

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.SavingsDeposit, -(long)amount, "Depósito na poupança");
        return (true, result.Money, result.Savings, result.SavingsStreak);
    }

    public async Task<(bool success, ulong wallet, ulong savings, ulong streak)> WithdrawSavingsAsync(ulong userId, ulong amount)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & Builders<EconomyProfile>.Filter.Gte(p => p.Savings, amount);

        var update = new BsonDocumentUpdateDefinition<EconomyProfile>(
            new BsonDocument
            {
                { "$inc", new BsonDocument { { "Savings", -(long)amount }, { "Money", (long)amount } } },
                { "$set", new BsonDocument("SavingsStreak", 0) }
            });

        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (result == null) return (false, 0, 0, 0);

        await AddTransactionAsync(userId, EconomyTransactionType.SavingsWithdraw, (long)amount, "Resgate da poupança");
        return (true, result.Money, result.Savings, result.SavingsStreak);
    }

    public async Task<bool> TryClaimWorkAsync(ulong userId, DateTime now, TimeSpan hours)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & (Builders<EconomyProfile>.Filter.Eq(p => p.LastWorkTime, null)
               | Builders<EconomyProfile>.Filter.Lte(p => p.LastWorkTime, now - hours));

        var update = Builders<EconomyProfile>.Update.Set(p => p.LastWorkTime, now);
        var result = await _collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> TryClaimRobAsync(ulong userId, DateTime now)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId)
            & (Builders<EconomyProfile>.Filter.Lte(p => p.RobCaughtUntil, now)
               | Builders<EconomyProfile>.Filter.Eq(p => p.RobCaughtUntil, null))
            & (Builders<EconomyProfile>.Filter.Lte(p => p.LastRobTime, now - EconomyRules.RobCooldown)
               | Builders<EconomyProfile>.Filter.Eq(p => p.LastRobTime, null));

        var update = Builders<EconomyProfile>.Update.Set(p => p.LastRobTime, now);
        var result = await _collection.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
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

    public async Task<EconomyProfile> SetDailyBoostAsync(ulong userId, bool pending)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update
            .Set(p => p.DailyBoostPending, pending)
            .Set(p => p.DailyBoostExpiresAt, pending ? DateTime.UtcNow.AddDays(7) : (DateTime?)null);
        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });
        return result!;
    }

    public async Task<EconomyProfile> SetWorkBoostAsync(ulong userId, bool pending)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update
            .Set(p => p.WorkBoostPending, pending)
            .Set(p => p.WorkBoostExpiresAt, pending ? DateTime.UtcNow.AddDays(7) : (DateTime?)null);
        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });
        return result!;
    }

    public async Task<EconomyProfile> SetRobShieldAsync(ulong userId, DateTime? until)
    {
        var filter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, userId);
        var update = Builders<EconomyProfile>.Update.Set(p => p.RobShieldUntil, until);
        var result = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EconomyProfile>
            {
                ReturnDocument = ReturnDocument.After
            });
        return result!;
    }

    public async Task<List<EconomyProfile>> GetTopUsersAsync(int limit)
    {
        var pipeline = new[]
        {
            new BsonDocument("$addFields", NetWorthProjection()),
            new BsonDocument("$match", new BsonDocument("NetWorth", new BsonDocument("$gt", new BsonDecimal128(0m)))),
            new BsonDocument("$sort", new BsonDocument("NetWorth", -1).Add("Money", -1)),
            new BsonDocument("$limit", limit),
            new BsonDocument("$project", new BsonDocument("NetWorth", 0))
        };

        return await _collection.Aggregate<EconomyProfile>(pipeline).ToListAsync();
    }

    private static BsonDocument NetWorthProjection()
        => new("NetWorth", new BsonDocument("$add", new BsonArray
        {
            new BsonDocument("$toDecimal", "$Money"),
            new BsonDocument("$toDecimal", "$Bank"),
            new BsonDocument("$toDecimal", "$Savings")
        }));

    public async Task<int> ApplyDailyMaintenanceAsync(IPetBonusProvider? petBonus = null)
    {
        var today = DateTime.UtcNow.Date;

        var bankFilter = Builders<EconomyProfile>.Filter.Gt(p => p.Bank, 1);
        var savingsFilter = Builders<EconomyProfile>.Filter.And(
            Builders<EconomyProfile>.Filter.Gt(p => p.Savings, 0),
            Builders<EconomyProfile>.Filter.Or(
                Builders<EconomyProfile>.Filter.Eq(p => p.SavingsLastInterestDate, null),
                Builders<EconomyProfile>.Filter.Lt(p => p.SavingsLastInterestDate, today)));

        var savingsPetBonus = new Dictionary<ulong, int>();
        if (petBonus != null)
        {
            var savingsUserIds = await _collection.Find(savingsFilter)
                .Project(p => p.UserId)
                .ToListAsync();
            if (savingsUserIds.Count > 0)
                savingsPetBonus = (await petBonus.GetUpgradePercentByMainIdsAsync(
                    savingsUserIds, UpgradeEffect.Savings)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        var filter = Builders<EconomyProfile>.Filter.Or(bankFilter, savingsFilter);
        var bulkOps = new List<WriteModel<EconomyProfile>>();
        var toLog = new List<EconomyTransaction>();
        int affected = 0;

        using var cursor = await _collection.Find(filter).ToCursorAsync();
        while (await cursor.MoveNextAsync())
        {
            foreach (var profile in cursor.Current)
            {
                var ops = new List<UpdateDefinition<EconomyProfile>>();

                if (profile.Bank > 1UL)
                {
                    var newBank = (ulong)Math.Ceiling(profile.Bank * 0.99);
                    if (newBank != profile.Bank)
                    {
                        ops.Add(Builders<EconomyProfile>.Update.Set(p => p.Bank, newBank));
                        toLog.Add(MakeTransaction(profile.UserId, EconomyTransactionType.Tax, (long)newBank - (long)profile.Bank, "Taxa bancária diária"));
                    }
                }

                if (profile.Savings > 0UL && (profile.SavingsLastInterestDate is null || profile.SavingsLastInterestDate.Value < today))
                {
                    var rate = EconomyRules.GetDailyInterestRate(Random.Shared, profile.SavingsStreak);
                    var interest = EconomyRules.ComputeInterestAmount(profile.Savings, rate);

                    savingsPetBonus.TryGetValue(profile.UserId, out var petPct);
                    if (petPct > 0)
                        interest += interest * (ulong)petPct / 100;

                    if (interest > 0UL)
                    {
                        ops.Add(Builders<EconomyProfile>.Update.Inc(p => p.Savings, interest));
                        ops.Add(Builders<EconomyProfile>.Update.Set(p => p.SavingsLastInterestDate, DateTime.UtcNow));
                        var petsSuffix = petPct > 0 ? $" (+{petPct}% de pet)" : string.Empty;
                        toLog.Add(MakeTransaction(profile.UserId, EconomyTransactionType.Interest, (long)interest,
                            $"Juros da poupança ({rate:P1} ao dia){petsSuffix}"));
                    }
                }

                if (ops.Count == 0) continue;

                var update = Builders<EconomyProfile>.Update.Combine(ops.ToArray());
                var userFilter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, profile.UserId);
                bulkOps.Add(new UpdateOneModel<EconomyProfile>(userFilter, update));
                affected++;

                if (bulkOps.Count < 500) continue;

                await _collection.BulkWriteAsync(bulkOps);
                if (toLog.Count > 0)
                    await _transactions.InsertManyAsync(toLog);
                bulkOps.Clear();
                toLog.Clear();
            }
        }

        if (bulkOps.Count > 0)
        {
            await _collection.BulkWriteAsync(bulkOps);
            if (toLog.Count > 0)
                await _transactions.InsertManyAsync(toLog);
        }

        return affected;
    }

    public async Task<List<EconomyTransaction>> GetHistoryAsync(ulong userId, int limit)
    {
        var filter = Builders<EconomyTransaction>.Filter.Eq(t => t.UserId, userId);
        return await _transactions.Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Limit(Math.Clamp(limit, 1, 50))
            .ToListAsync();
    }

    public async Task<UnifyResult?> UnifyProfileAsync(ulong sourceUserId, ulong targetUserId)
    {
        var profileFilter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, sourceUserId);
        var source = await _collection.Find(profileFilter).FirstOrDefaultAsync();
        if (source == null) return null;

        var targetFilter = Builders<EconomyProfile>.Filter.Eq(p => p.UserId, targetUserId);
        var target = await _collection.Find(targetFilter).FirstOrDefaultAsync();

        UnifyResult result;
        if (target != null)
        {
            result = EconomyUnifier.Unify(source, target);
            await _collection.ReplaceOneAsync(targetFilter, target);
        }
        else
        {
            target = new EconomyProfile { UserId = targetUserId };
            result = EconomyUnifier.Unify(source, target);
            await _collection.InsertOneAsync(target);
        }

        var txFilter = Builders<EconomyTransaction>.Filter.Eq(t => t.UserId, sourceUserId);
        var txUpdate = Builders<EconomyTransaction>.Update.Set(t => t.UserId, targetUserId);
        await _transactions.UpdateManyAsync(txFilter, txUpdate);

        await AddTransactionAsync(targetUserId, EconomyTransactionType.Merge, (long)result.MergedMoney,
            $"Unificação de conta vinculada");

        await _collection.DeleteOneAsync(profileFilter);
        return result;
    }

    public async Task LogTransactionAsync(ulong userId, EconomyTransactionType type, long amount, string description)
    {
        await AddTransactionAsync(userId, type, amount, description);
    }

    private async Task AddTransactionAsync(ulong userId, EconomyTransactionType type, long amount, string description)
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

    private static EconomyTransaction MakeTransaction(ulong userId, EconomyTransactionType type, long amount, string description)
        => new()
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
}