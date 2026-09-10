using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IShopRepository
{
    Task EnsureIndexesAsync();
    Task<List<ShopItem>> GetAllAsync();
    Task<ShopItem?> GetByKeyAsync(string key);
    Task UpsertAsync(ShopItem item);
    Task DeleteAsync(string key);
    Task<long> CountAsync();
    Task<List<InventoryItem>> GetInventoryAsync(ulong userId);
    Task<List<InventoryItem>> GetPetLevelsAsync(IEnumerable<ulong> userIds, IEnumerable<string> petKeys);
    Task<InventoryItem?> GetInventoryByKeyAsync(ulong userId, string itemKey);
    Task AddOrIncrementInventoryAsync(InventoryItem inventory);
    Task DecrementOrRemoveInventoryAsync(ulong userId, string itemKey);
    Task SetPetNicknameAsync(ulong userId, string itemKey, string? nickname);
    Task UpdateIncomeTimestampAsync(ulong userId, string itemKey, DateTime collectedAt);
    Task UnequipAllAsync(ulong userId);
    Task SetEquippedAsync(ulong userId, string itemKey, bool equipped);
    Task MigrateInventoryAsync(ulong sourceUserId, ulong targetUserId);
}
