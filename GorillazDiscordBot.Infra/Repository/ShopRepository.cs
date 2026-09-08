using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class ShopRepository : IShopRepository
{
    private readonly IMongoCollection<ShopItem> _items;
    private readonly IMongoCollection<InventoryItem> _inventory;

    public ShopRepository(IOptions<MongoOptions> options)
    {
        MongoMappings.Register();
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        _items = database.GetCollection<ShopItem>(nameof(ShopItem));
        _inventory = database.GetCollection<InventoryItem>(nameof(InventoryItem));
    }

    public async Task<List<ShopItem>> GetAllAsync()
        => await _items.Find(_ => true).SortBy(i => i.SortOrder).ToListAsync();

    public async Task<ShopItem?> GetByKeyAsync(string key)
    {
        var filter = Builders<ShopItem>.Filter.Eq(i => i.Key, key);
        return await _items.Find(filter).FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(ShopItem item)
    {
        var filter = Builders<ShopItem>.Filter.Eq(i => i.Key, item.Key);
        var options = new ReplaceOptions { IsUpsert = true };
        await _items.ReplaceOneAsync(filter, item, options);
    }

    public async Task DeleteAsync(string key)
    {
        var filter = Builders<ShopItem>.Filter.Eq(i => i.Key, key);
        await _items.DeleteOneAsync(filter);
    }

    public async Task<long> CountAsync()
        => await _items.CountDocumentsAsync(_ => true);

    public async Task<List<InventoryItem>> GetInventoryAsync(ulong userId)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId);
        return await _inventory.Find(filter).ToListAsync();
    }

    public async Task<InventoryItem?> GetInventoryByKeyAsync(ulong userId, string itemKey)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId)
            & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, itemKey);
        return await _inventory.Find(filter).FirstOrDefaultAsync();
    }

    public async Task AddOrIncrementInventoryAsync(InventoryItem inventory)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, inventory.UserId)
            & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, inventory.ItemKey);

        var existing = await _inventory.Find(filter).FirstOrDefaultAsync();
        if (existing == null)
        {
            await _inventory.InsertOneAsync(inventory);
            return;
        }

        var update = Builders<InventoryItem>.Update
            .Inc(i => i.Quantity, inventory.Quantity)
            .Set(i => i.ExpiresAt, inventory.ExpiresAt);
        await _inventory.UpdateOneAsync(filter, update);
    }

    public async Task DecrementOrRemoveInventoryAsync(ulong userId, string itemKey)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId)
            & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, itemKey);

        var existing = await _inventory.Find(filter).FirstOrDefaultAsync();
        if (existing == null) return;

        if (existing.Quantity <= 1)
        {
            await _inventory.DeleteOneAsync(filter);
            return;
        }

        var update = Builders<InventoryItem>.Update.Inc(i => i.Quantity, -1);
        await _inventory.UpdateOneAsync(filter, update);
    }

    public async Task UpdateIncomeTimestampAsync(ulong userId, string itemKey, DateTime collectedAt)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId)
            & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, itemKey);

        var update = Builders<InventoryItem>.Update.Set(i => i.LastCollectedAt, collectedAt);
        await _inventory.UpdateOneAsync(filter, update);
    }

    public async Task UnequipAllAsync(ulong userId)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId);
        var update = Builders<InventoryItem>.Update.Set(i => i.IsEquipped, false);
        await _inventory.UpdateManyAsync(filter, update);
    }

    public async Task SetEquippedAsync(ulong userId, string itemKey, bool equipped)
    {
        var filter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, userId)
            & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, itemKey);
        var update = Builders<InventoryItem>.Update.Set(i => i.IsEquipped, equipped);
        await _inventory.UpdateOneAsync(filter, update);
    }

    public async Task MigrateInventoryAsync(ulong sourceUserId, ulong targetUserId)
    {
        var sourceFilter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, sourceUserId);
        var sourceItems = await _inventory.Find(sourceFilter).ToListAsync();
        if (sourceItems.Count == 0) return;

        foreach (var item in sourceItems)
        {
            var targetFilter = Builders<InventoryItem>.Filter.Eq(i => i.UserId, targetUserId)
                & Builders<InventoryItem>.Filter.Eq(i => i.ItemKey, item.ItemKey);
            var existing = await _inventory.Find(targetFilter).FirstOrDefaultAsync();

            if (existing == null)
            {
                item.Id = string.Empty;
                item.UserId = targetUserId;
                await _inventory.InsertOneAsync(item);
                continue;
            }

            var update = Builders<InventoryItem>.Update
                .Inc(i => i.Quantity, item.Quantity)
                .Set(i => i.IsEquipped, existing.IsEquipped || item.IsEquipped)
                .Set(i => i.ExpiresAt, Later(item.ExpiresAt, existing.ExpiresAt))
                .Set(i => i.AcquiredAt, Earlier(item.AcquiredAt, existing.AcquiredAt))
                .Set(i => i.LastCollectedAt, Earlier(item.LastCollectedAt, existing.LastCollectedAt));
            await _inventory.UpdateOneAsync(targetFilter, update);
        }

        await _inventory.DeleteManyAsync(sourceFilter);
    }

    private static DateTime? Later(DateTime? a, DateTime? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return a.Value >= b.Value ? a : b;
    }

    private static DateTime? Earlier(DateTime? a, DateTime? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        return a.Value <= b.Value ? a : b;
    }
}
