using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public sealed record AssetIncomeEntry(string Emoji, string Name, int Days, ulong Income);

public sealed class AssetIncomesResult
{
    public ulong TotalIncome { get; init; }
    public int DaysCollected { get; init; }
    public int ItemsCollected { get; init; }
    public int PetBonusPercent { get; init; }
    public IReadOnlyList<AssetIncomeEntry> Assets { get; init; } = [];
}

public class ShopService
{
    public static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(10);
    public const double SellRefundRate = 0.5;
    public const int MaxIncomeBacklogDays = 3;

    private readonly IShopRepository _shop;
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly ICharacterProfileRepository _profiles;

    private readonly object _lock = new();
    private List<ShopItem>? _cached;
    private DateTime _cacheExpiresAt;

    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _userLocks = new();

    private SemaphoreSlim GetUserLock(ulong mainId)
        => _userLocks.GetOrAdd(mainId, static _ => new SemaphoreSlim(1, 1));

    public ShopService(
        IShopRepository shop,
        IEconomyRepository economy,
        IEconomyAccessor accessor,
        ICharacterProfileRepository profiles)
    {
        _shop = shop;
        _economy = economy;
        _accessor = accessor;
        _profiles = profiles;
    }

    public async Task<List<ShopItem>> GetCatalogAsync()
    {
        lock (_lock)
        {
            if (_cached != null && DateTime.UtcNow < _cacheExpiresAt)
                return _cached;
        }

        var items = await _shop.GetAllAsync();

        lock (_lock)
        {
            _cached = items;
            _cacheExpiresAt = DateTime.UtcNow.Add(CatalogCacheDuration);
            return _cached;
        }
    }

    public async Task<ShopItem?> FindItemAsync(string keyOrName)
    {
        var key = keyOrName.Trim().ToLowerInvariant();
        var items = await GetCatalogAsync();
        return items.FirstOrDefault(i =>
            i.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            || i.Name.Equals(keyOrName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void ForceReload()
    {
        lock (_lock)
        {
            _cached = null;
            _cacheExpiresAt = DateTime.MinValue;
        }
    }

    public async Task UpsertItemAsync(ShopItem item)
    {
        await _shop.UpsertAsync(item);
        ForceReload();
    }

    public async Task<bool> RemoveItemAsync(string key)
    {
        var item = await _shop.GetByKeyAsync(key);
        if (item == null) return false;

        await _shop.DeleteAsync(key);
        ForceReload();
        return true;
    }

    public async Task SeedIfEmptyAsync()
    {
        var existing = await _shop.GetAllAsync();
        var existingKeys = existing.Select(i => i.Key).ToHashSet();

        var defaults = DefaultCatalog().ToList();
        foreach (var item in defaults)
        {
            if (existingKeys.Contains(item.Key))
                continue;

            await _shop.UpsertAsync(item);
        }

        await MigrateStoredPetsAsync(defaults);

        ForceReload();
    }

    private async Task MigrateStoredPetsAsync(IEnumerable<ShopItem> defaults)
    {
        foreach (var pet in defaults.Where(i => i.Category == ItemCategory.Pet))
        {
            var stored = await _shop.GetByKeyAsync(pet.Key);
            if (stored is null || stored.UpgradeEffect != UpgradeEffect.None)
                continue;

            await _shop.UpsertAsync(pet);
        }
    }

    public async Task<(bool success, string? message, ulong balance)> BuyAsync(
        ulong userId, string username, ShopItem item)
    {
        if (item.IsPlaceholder)
            return (false, $"🔒 **{item.Name}** ainda não está disponível. Volte em breve!", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var gate = GetUserLock(mainId);
        await gate.WaitAsync();
        try
        {
            return await BuyCoreAsync(mainId, username, item);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool success, string? message, ulong balance)> BuyCoreAsync(
        ulong mainId, string username, ShopItem item)
    {
        await _economy.GetOrCreateAsync(mainId, username);

        if (item.Category == ItemCategory.Asset)
            return (false, "📈 Ativos agora são comprados por cotas: use `/banco ativos comprar`.", 0);

        if (item.Category == ItemCategory.Pet)
        {
            var inventory = await _shop.GetInventoryAsync(mainId);
            var ownedPetKeys = inventory
                .Select(i => i.ItemKey)
                .ToHashSet();

            if (!ownedPetKeys.Contains(item.Key))
            {
                var catalog = await GetCatalogAsync();
                var petTypeCount = inventory
                    .Count(i => catalog.Any(c => c.Key == i.ItemKey && c.Category == ItemCategory.Pet));
                const int MaxPetTypes = 2;

                if (petTypeCount >= MaxPetTypes)
                    return (false, $"🐾 Você já possui **{MaxPetTypes} tipos de pets**. Forme 1 deles para adotar um novo.", 0);
            }
        }

        if (item.MaxQuantity > 0)
        {
            var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
            if (owned != null && owned.Quantity >= item.MaxQuantity)
                return (false, $"❌ Você já atingiu o limite de **{item.MaxQuantity}** unidade(s) de **{item.Emoji} {item.Name}**.", 0);
        }

        var (deducted, balance) = await _economy.TryDeductMoneyAsync(
            mainId, item.Price, EconomyTransactionType.Purchase,
            $"Comprou {item.Name}");

        if (!deducted)
            return (false, "❌ Você não tem moedas suficientes na carteira.", 0);

        var now = DateTime.UtcNow;
        var expiresAt = item.DurationHours > 0
            ? (DateTime?)now.AddHours(item.DurationHours)
            : null;

        await _shop.AddOrIncrementInventoryAsync(new InventoryItem
        {
            UserId = mainId,
            ItemKey = item.Key,
            Quantity = 1,
            ExpiresAt = expiresAt,
            AcquiredAt = now,
            LastCollectedAt = now
        });

        return (true, null, balance);
    }

    public async Task<(bool success, string? message, ulong balance)> BuyAssetAsync(
        ulong userId, string username, ShopItem item, int cotas)
    {
        if (item.IsPlaceholder)
            return (false, $"🔒 **{item.Name}** ainda não está disponível. Volte em breve!", 0);

        if (cotas < 1)
            return (false, "❌ Informe ao menos **1 cota**.", 0);

        if (cotas > EconomyRules.AssetQuotasPerShare)
            return (false, $"❌ O limite é de **{EconomyRules.AssetQuotasPerShare} cotas** por ativo.", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var gate = GetUserLock(mainId);
        await gate.WaitAsync();
        try
        {
            return await BuyAssetCoreAsync(mainId, username, item, cotas);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool success, string? message, ulong balance)> BuyAssetCoreAsync(
        ulong mainId, string username, ShopItem item, int cotas)
    {
        await _economy.GetOrCreateAsync(mainId, username);

        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned is { Quantity: 1 })
        {
            owned.Quantity = EconomyRules.AssetQuotasPerShare;
            await _shop.SetQuantityAsync(mainId, item.Key, owned.Quantity);
        }

        var held = owned?.Quantity ?? 0;
        if (held + cotas > EconomyRules.AssetQuotasPerShare)
            return (false, $"❌ Você já possui **{held} cotas** de **{item.Emoji} {item.Name}**. Limite: **{EconomyRules.AssetQuotasPerShare} cotas**.", 0);

        var price = EconomyRules.ComputeQuotaPrice(item.Key, item.Price, cotas);

        var (deducted, balance) = await _economy.TryDeductMoneyAsync(
            mainId, price, EconomyTransactionType.Purchase,
            $"Comprou {cotas} cotas de {item.Name}");

        if (!deducted)
            return (false, "❌ Você não tem moedas suficientes na carteira.", 0);

        var now = DateTime.UtcNow;
        await _shop.AddOrIncrementInventoryAsync(new InventoryItem
        {
            UserId = mainId,
            ItemKey = item.Key,
            Quantity = cotas,
            ExpiresAt = null,
            AcquiredAt = now,
            LastCollectedAt = now
        });

        return (true, null, balance);
    }

    public async Task<(bool success, string? message, ulong balance)> SellAssetAsync(
        ulong userId, string username, ShopItem item, int cotas)
    {
        if (cotas < 1)
            return (false, "❌ Informe ao menos **1 cota** para vender.", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var gate = GetUserLock(mainId);
        await gate.WaitAsync();
        try
        {
            return await SellAssetCoreAsync(mainId, username, item, cotas);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool success, string? message, ulong balance)> SellAssetCoreAsync(
        ulong mainId, string username, ShopItem item, int cotas)
    {
        await _economy.GetOrCreateAsync(mainId, username);

        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned is { Quantity: 1 })
        {
            owned.Quantity = EconomyRules.AssetQuotasPerShare;
            await _shop.SetQuantityAsync(mainId, item.Key, owned.Quantity);
        }

        var held = owned?.Quantity ?? 0;
        if (held <= 0)
            return (false, $"❌ Você não possui **{item.Emoji} {item.Name}**. Use `/banco ativos comprar`.", 0);

        if (cotas > held)
            return (false, $"❌ Você possui apenas **{held} cotas** de **{item.Emoji} {item.Name}**.", 0);

        var refund = EconomyRules.ComputeQuotaPrice(item.Key, item.Price, cotas);
        await _economy.AddMoneyAsync(mainId, refund, EconomyTransactionType.Sell,
            $"Vendeu {cotas} cota(s) de {item.Name} pelo preço do dia");

        await _shop.DecrementOrRemoveInventoryAsync(mainId, item.Key, cotas);

        var balance = (await _economy.GetOrCreateAsync(mainId, username)).Money;
        return (true, null, balance);
    }

    public async Task<List<(InventoryItem Owned, ShopItem Asset)>> GetOwnedAssetPositionsAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var catalog = await GetCatalogAsync();
        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        var inventory = await _shop.GetInventoryAsync(mainId);

        await NormalizeLegacyAssetQuantitiesAsync(mainId, inventory, catalogByKey);

        return inventory
            .Where(e => e.Quantity > 0
                        && catalogByKey.TryGetValue(e.ItemKey, out var it)
                        && it.Category == ItemCategory.Asset)
            .Select(e => (Owned: e, Asset: catalogByKey[e.ItemKey]))
            .OrderBy(x => x.Asset.SortOrder)
            .ToList();
    }

    private async Task NormalizeLegacyAssetQuantitiesAsync(
        ulong mainId, List<InventoryItem> inventory, Dictionary<string, ShopItem> catalogByKey)
    {
        foreach (var entry in inventory)
        {
            if (!catalogByKey.TryGetValue(entry.ItemKey, out var item)) continue;
            if (item.Category != ItemCategory.Asset) continue;
            if (entry.Quantity != 1) continue;

            entry.Quantity = EconomyRules.AssetQuotasPerShare;
            await _shop.SetQuantityAsync(mainId, entry.ItemKey, entry.Quantity);
        }
    }

    public async Task<ulong> GetBalanceAsync(ulong userId, string username)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _economy.GetOrCreateAsync(mainId, username);
        return profile.Money;
    }

    public async Task<AssetIncomesResult> ApplyAssetIncomesAsync(
        ulong userId, string username, bool boosted = false)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        await _economy.GetOrCreateAsync(mainId, username);

        var catalog = await GetCatalogAsync();
        var assetKeys = catalog
            .Where(i => i.Category == ItemCategory.Asset && i.DailyIncome > 0)
            .Select(i => i.Key)
            .ToHashSet();

        if (assetKeys.Count == 0)
            return new AssetIncomesResult();

        var inventory = await _shop.GetInventoryAsync(mainId);
        var now = DateTime.UtcNow;
        var assetPetBonus = await GetUpgradePercentCoreAsync(mainId, UpgradeEffect.AssetIncome);
        ulong total = 0;
        int daysTotal = 0;
        var entries = new List<AssetIncomeEntry>();

        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        await NormalizeLegacyAssetQuantitiesAsync(mainId, inventory, catalogByKey);

        foreach (var entry in inventory)
        {
            if (!assetKeys.Contains(entry.ItemKey)) continue;
            var item = catalog.First(i => i.Key == entry.ItemKey);

            var baseline = entry.LastCollectedAt ?? entry.AcquiredAt ?? now;
            var days = (int)Math.Floor((now - baseline).TotalDays);
            if (days <= 0) continue;

            days = Math.Min(days, MaxIncomeBacklogDays);
            var income = EconomyRules.ComputeQuotaIncome(item.DailyIncome, Math.Max(0, entry.Quantity)) * (ulong)days;
            if (boosted)
                income *= 2;
            if (assetPetBonus > 0)
                income += income * (ulong)assetPetBonus / 100;

            if (income > 0)
            {
                await _economy.AddMoneyAsync(mainId, income, EconomyTransactionType.Income,
                    $"Renda de {days} dia(s) — {item.Name}");
                await _shop.UpdateIncomeTimestampAsync(mainId, item.Key, now);

                total += income;
                daysTotal += days;
                entries.Add(new AssetIncomeEntry(item.Emoji, item.Name, days, income));
            }
        }

        return new AssetIncomesResult
        {
            TotalIncome = total,
            DaysCollected = daysTotal,
            ItemsCollected = entries.Count,
            PetBonusPercent = assetPetBonus,
            Assets = entries
        };
    }

    public async Task<(bool success, string? message, ulong balance)> SellAsync(
        ulong userId, string username, ShopItem item)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var gate = GetUserLock(mainId);
        await gate.WaitAsync();
        try
        {
            return await SellCoreAsync(mainId, username, item);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool success, string? message, ulong balance)> SellCoreAsync(
        ulong mainId, string username, ShopItem item)
    {
        if (item.Category == ItemCategory.Asset)
            return (false, "📈 Ativos agora são vendidos por cotas: use `/banco ativos vender`.", 0);

        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, "❌ Você não possui esse item para vender.", 0);

        var refund = (ulong)Math.Floor(item.Price * SellRefundRate);
        await _economy.AddMoneyAsync(mainId, refund, EconomyTransactionType.Sell,
            $"Vendeu {item.Name}");
        await _shop.DecrementOrRemoveInventoryAsync(mainId, item.Key);

        if (item.Category == ItemCategory.Vehicle)
        {
            var profile = await _profiles.GetAsync(mainId);
            if (profile is { VeiculoAtualKey: not null } && profile.VeiculoAtualKey == item.Key)
            {
                profile.VeiculoAtualKey = null;
                await _profiles.SaveAsync(profile);
            }
        }

        var balance = (await _economy.GetOrCreateAsync(mainId, username)).Money;
        return (true, null, balance);
    }

    public async Task<(bool success, string? message, ulong balance)> UseAsync(
        ulong userId, string username, ShopItem item)
    {
        if (item.Category != ItemCategory.Boost || item.Effect == BoostEffect.None)
            return (false, item.Category == ItemCategory.Asset
                ? "📈 Este é um ativo de renda passiva: ele rende automaticamente junto com o `daily`, não precisa usar."
                : item.Category == ItemCategory.Relic
                ? "⌚ Este é um relógio equipável: use `equipar <id>` para ativar o bônus."
                : item.Category == ItemCategory.Pet
                ? "🐾 Este é um pet passivo: cada cópia aumenta o nível e o bônus permanentemente."
                : item.Category == ItemCategory.Vehicle
                ? "🚗 Este é um veículo: use `/veiculo dirigir <id>` para equipá-lo."
                : item.Category == ItemCategory.Upgrade
                ? "🅿️ Esta é uma melhoria permanente: o bônus já vale automaticamente no manobrista."
                : "🎨 Este item é cosmético e não pode ser usado.", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var gate = GetUserLock(mainId);
        await gate.WaitAsync();
        try
        {
            return await UseCoreAsync(mainId, username, item);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool success, string? message, ulong balance)> UseCoreAsync(
        ulong mainId, string username, ShopItem item)
    {
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, "❌ Você não possui esse item para usar.", 0);

        await _shop.DecrementOrRemoveInventoryAsync(mainId, item.Key);

        switch (item.Effect)
        {
            case BoostEffect.DailyX2:
                await _economy.SetDailyBoostAsync(mainId, true);
                break;
            case BoostEffect.WorkX2:
                await _economy.SetWorkBoostAsync(mainId, true);
                break;
            case BoostEffect.RobShield:
                var profile = await _economy.GetOrCreateAsync(mainId, username);
                var anchor = profile.RobShieldUntil is { } s && s > DateTime.UtcNow ? s : DateTime.UtcNow;
                await _economy.SetRobShieldAsync(mainId, anchor.AddHours(item.DurationHours));
                break;
        }

        await _economy.LogTransactionAsync(mainId, EconomyTransactionType.BoostUse, 0, $"Usou boost {item.Name}");

        var balance = (await _economy.GetOrCreateAsync(mainId, username)).Money;
        return (true, null, balance);
    }

    public async Task<List<InventoryItem>> GetInventoryAsync(ulong userId)
        => await _shop.GetInventoryAsync(await _accessor.ResolveMainIdAsync(userId));

    public async Task<IReadOnlySet<VehicleType>> GetOwnedVehicleTypesAsync(ulong userId)
        => await GetOwnedVehicleTypesCoreAsync(await _accessor.ResolveMainIdAsync(userId));

    private async Task<IReadOnlySet<VehicleType>> GetOwnedVehicleTypesCoreAsync(ulong mainId)
    {
        var catalog = await GetCatalogAsync();
        var inventory = await _shop.GetInventoryAsync(mainId);
        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        return inventory
            .Where(e => e.Quantity > 0)
            .Select(e => catalogByKey.TryGetValue(e.ItemKey, out var item) ? item : null)
            .Where(i => i is { Category: ItemCategory.Vehicle } && i.VehicleType != VehicleType.None)
            .Select(i => i!.VehicleType)
            .ToHashSet();
    }

    public async Task<(bool success, string? message)> EquipAsync(ulong userId, string username, string keyOrName)
    {
        var item = await FindItemAsync(keyOrName);
        if (item is not { Category: ItemCategory.Relic or ItemCategory.Weapon or ItemCategory.Equipment })
            return (false, "❌ Esse item não é um relógio, arma ou equipamento equipável. Use `usar` para boosts ou `comprar` para adquirir.");

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, $"❌ Você não possui **{item.Emoji} {item.Name}** para equipar.");

        if (item.Category == ItemCategory.Relic)
        {
            await _shop.UnequipAllAsync(mainId);
            await _shop.SetEquippedAsync(mainId, item.Key, true);

            var currentProfile = await _profiles.GetAsync(mainId);
            if (currentProfile?.ArmaAtualKey is { } wKey)
                await _shop.SetEquippedAsync(mainId, wKey, true);
            if (currentProfile?.EquipamentoAtualKey is { } eqKey)
                await _shop.SetEquippedAsync(mainId, eqKey, true);

            return (true, $"⌚ **{item.Name}** equipado! Bônus ativo no cassino.");
        }

        if (item.Category == ItemCategory.Weapon)
        {
            var profile = await _profiles.GetOrCreateAsync(mainId, username);
            if (profile.ArmaAtualKey is { } prevWeapon && !prevWeapon.Equals(item.Key, StringComparison.OrdinalIgnoreCase))
            {
                await _shop.SetEquippedAsync(mainId, prevWeapon, false);
            }

            profile.ArmaAtualKey = item.Key;
            await _profiles.SaveAsync(profile);
            await _shop.SetEquippedAsync(mainId, item.Key, true);
            return (true, $"{item.Emoji} **{item.Name}** equipada! Pronta para o combate.");
        }

        if (item.Category == ItemCategory.Equipment)
        {
            var profile = await _profiles.GetOrCreateAsync(mainId, username);
            if (profile.EquipamentoAtualKey is { } prevEquip && !prevEquip.Equals(item.Key, StringComparison.OrdinalIgnoreCase))
            {
                await _shop.SetEquippedAsync(mainId, prevEquip, false);
            }

            profile.EquipamentoAtualKey = item.Key;
            await _profiles.SaveAsync(profile);
            await _shop.SetEquippedAsync(mainId, item.Key, true);
            return (true, $"{item.Emoji} **{item.Name}** equipado! Efeito utilitário ativo.");
        }

        return (false, "❌ Categoria não equipável.");
    }

    public async Task<(bool success, string? message)> UnequipAsync(ulong userId, string keyOrName)
    {
        var item = await FindItemAsync(keyOrName);
        if (item == null)
            return (false, "❌ Item não encontrado. Use `equipar <id>` para ver os ids disponíveis.");

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || !owned.IsEquipped)
            return (false, "❌ Esse item não está equipado.");

        await _shop.SetEquippedAsync(mainId, item.Key, false);

        if (item.Category == ItemCategory.Weapon)
        {
            var profile = await _profiles.GetAsync(mainId);
            if (profile is { ArmaAtualKey: not null })
            {
                profile.ArmaAtualKey = null;
                await _profiles.SaveAsync(profile);
            }
        }
        else if (item.Category == ItemCategory.Equipment)
        {
            var profile = await _profiles.GetAsync(mainId);
            if (profile is { EquipamentoAtualKey: not null })
            {
                profile.EquipamentoAtualKey = null;
                await _profiles.SaveAsync(profile);
            }
        }

        return (true, $"{item.Emoji} **{item.Name}** desequipado.");
    }

    public async Task<(bool success, string? message)> DriveVehicleAsync(
        ulong userId, string username, string keyOrName)
    {
        var item = await FindItemAsync(keyOrName);
        if (item == null)
            return (false, "❌ Veículo não encontrado. Use `/garagem` para ver os seus.");

        if (item.Category != ItemCategory.Vehicle)
            return (false, "❌ Esse item não é um veículo. Use `equipar <id>` para relógios.");

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, $"❌ Você não possui **{item.Emoji} {item.Name}**. Compre na `/loja`.");

        var profile = await _profiles.GetOrCreateAsync(mainId, username);

        var required = VehicleRules.RequiredLicense(item);
        if (required is not { } license)
            return (false, "❌ Este veículo não exige licença e não pode ser dirigido.");

        if (!profile.Licencas.Contains(license))
        {
            return (false,
                $"🚗 Para dirigir **{item.Emoji} {item.Name}** você precisa da licença {VehicleRules.FormatRequirement(license)}. Use `/veiculo licenca prova`.");
        }

        profile.VeiculoAtualKey = item.Key;
        await _profiles.SaveAsync(profile);

        return (true, $"🚗 **{item.Emoji} {item.Name}** equipado! Vamos acelerar!");
    }

    public async Task<(bool success, string? message)> ParkVehicleAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _profiles.GetAsync(mainId);
        if (profile is not { VeiculoAtualKey: not null })
            return (false, "🅿️ Você não está dirigindo nenhum veículo.");

        profile.VeiculoAtualKey = null;
        await _profiles.SaveAsync(profile);
        return (true, "🅿️ Veículo guardado na garagem.");
    }

    public async Task<(ShopItem? vehicle, string? key)> GetCurrentVehicleAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _profiles.GetAsync(mainId);
        if (profile is not { VeiculoAtualKey: { } key })
            return (null, null);

        var item = await FindItemAsync(key);
        return (item, key);
    }

    public async Task<(ShopItem? relic, int value, bool cashback)> GetEquippedRelicAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var inventory = await _shop.GetInventoryAsync(mainId);
        var equipped = inventory.FirstOrDefault(i => i.IsEquipped);
        if (equipped == null)
            return (null, 0, false);

        var item = await FindItemAsync(equipped.ItemKey);
        if (item is not { Category: ItemCategory.Relic } || item.RelicEffect == RelicEffect.None)
            return (null, 0, false);

        return (item, item.RelicValue, item.RelicEffect == RelicEffect.Cashback);
    }

    public async Task<(ShopItem? weapon, int attackBonus, int defenseBonus, ulong maxStealBonus)> GetEquippedWeaponAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _profiles.GetAsync(mainId);
        if (profile?.ArmaAtualKey is { } key)
        {
            var item = await FindItemAsync(key);
            if (item != null && item.Category == ItemCategory.Weapon)
                return (item, item.CrimeBonusPercent, item.CrimeDefensePercent, item.CrimeMaxStealBonus);
        }

        var inventory = await _shop.GetInventoryAsync(mainId);
        foreach (var inv in inventory.Where(i => i.IsEquipped))
        {
            var item = await FindItemAsync(inv.ItemKey);
            if (item is { Category: ItemCategory.Weapon })
                return (item, item.CrimeBonusPercent, item.CrimeDefensePercent, item.CrimeMaxStealBonus);
        }

        return (null, 0, 0, 0);
    }

    public async Task<(ShopItem? equipment, EquipmentType type, int bonus, int defense)> GetEquippedEquipmentAsync(ulong userId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _profiles.GetAsync(mainId);
        if (profile?.EquipamentoAtualKey is { } key)
        {
            var item = await FindItemAsync(key);
            if (item != null && item.Category == ItemCategory.Equipment)
                return (item, item.EquipmentType, item.CrimeBonusPercent, item.CrimeDefensePercent);
        }

        var inventory = await _shop.GetInventoryAsync(mainId);
        foreach (var inv in inventory.Where(i => i.IsEquipped))
        {
            var item = await FindItemAsync(inv.ItemKey);
            if (item is { Category: ItemCategory.Equipment })
                return (item, item.EquipmentType, item.CrimeBonusPercent, item.CrimeDefensePercent);
        }

        return (null, EquipmentType.None, 0, 0);
    }

    public async Task<int> GetUpgradePercentAsync(ulong userId, UpgradeEffect effect)
        => await GetUpgradePercentCoreAsync(await _accessor.ResolveMainIdAsync(userId), effect);

    public async Task<int> GetUpgradeFlatAsync(ulong userId, UpgradeEffect effect)
        => await GetUpgradeFlatCoreAsync(await _accessor.ResolveMainIdAsync(userId), effect);

    public async Task<ulong> GetInventoryValueAsync(ulong userId)
        => await GetInventoryValueCoreAsync(await _accessor.ResolveMainIdAsync(userId));

    private async Task<ulong> GetInventoryValueCoreAsync(ulong mainId)
    {
        var catalog = await GetCatalogAsync();
        var inventory = await _shop.GetInventoryAsync(mainId);
        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        await NormalizeLegacyAssetQuantitiesAsync(mainId, inventory, catalogByKey);

        ulong total = 0;
        foreach (var entry in inventory)
        {
            if (entry.Quantity <= 0) continue;
            if (!catalogByKey.TryGetValue(entry.ItemKey, out var item)) continue;
            if (item.Price == 0) continue;

            if (item.Category == ItemCategory.Asset)
                total = CheckedAdd(total, EconomyRules.ComputeQuotaPrice(item.Key, item.Price, entry.Quantity));
            else
                total = CheckedAdd(total, item.Price * (ulong)entry.Quantity);
        }

        return total;
    }

    private static ulong CheckedAdd(ulong a, ulong b)
    {
        var sum = a + b;
        return sum < a ? ulong.MaxValue : sum;
    }

    private async Task<int> GetUpgradePercentCoreAsync(ulong mainId, UpgradeEffect effect)
    {
        if (effect == UpgradeEffect.None) return 0;

        var catalog = await GetCatalogAsync();
        var inventory = await _shop.GetInventoryAsync(mainId);
        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);

        var total = 0;
        foreach (var entry in inventory)
        {
            if (entry.Quantity <= 0) continue;
            if (!catalogByKey.TryGetValue(entry.ItemKey, out var item)) continue;
            if (item is not { Category: ItemCategory.Pet } || item.UpgradeEffect != effect) continue;

            total += item.UpgradeValue * entry.Quantity;
        }

        return total;
    }

    private async Task<int> GetUpgradeFlatCoreAsync(ulong mainId, UpgradeEffect effect)
    {
        if (effect == UpgradeEffect.None) return 0;

        var catalog = await GetCatalogAsync();
        var inventory = await _shop.GetInventoryAsync(mainId);
        var catalogByKey = catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var total = 0;
        foreach (var entry in inventory)
        {
            if (entry.Quantity <= 0) continue;
            if (entry.ExpiresAt is { } exp && exp <= now) continue;
            if (!catalogByKey.TryGetValue(entry.ItemKey, out var item)) continue;
            if (item is not { Category: ItemCategory.Upgrade } || item.UpgradeEffect != effect) continue;

            total += item.UpgradeValue * entry.Quantity;
        }

        return total;
    }

    private static IEnumerable<ShopItem> DefaultCatalog()
    {
        var seed = new List<ShopItem>
        {
            NewItem("banana", "Banana Dourada", "🍌", "Item de coleção do Gorillaz.", 1000, ItemCategory.Cosmetic, 1),
            NewItem("bone", "Boné do Gorillaz", "🧢", "Um boné exclusivo de coleção.", 2500, ItemCategory.Cosmetic, 2),
            NewItem("trofeu", "Troféu Prime", "🏆", "Mostre que você é o macaco alfa.", 5000, ItemCategory.Cosmetic, 3),
            NewItem("camisa", "Camisa 2D", "🎤", "A camiseta oficial do vocalista.", 7500, ItemCategory.Cosmetic, 4),
            NewItem("mascara", "Máscara de Murdoc", "🎭", "A máscara do baixista, peça rara.", 12000, ItemCategory.Cosmetic, 5),
            NewItem("dailyx2", "Luvas de Ouro", "⚡", "Seu próximo daily rende o DOBRO.", 3000, ItemCategory.Boost, 6, BoostEffect.DailyX2),
            NewItem("workx2", "Capacete Turbo", "💼", "Seu próximo trabalho rende o DOBRO.", 4000, ItemCategory.Boost, 7, BoostEffect.WorkX2),
            NewItem("escudo", "Escudo Anti-Roubo", "🛡️", "Fica imune a roubos por 24h.", 2500, ItemCategory.Boost, 8, BoostEffect.RobShield, 24),

            NewAsset("acoes", "Ações da Fazenda", "📈", "Ações que rendem dividendos diários.", 20000, 2000, 9),
            NewAsset("fazenda", "Fazenda Gorillaz", "🚜", "Produz bananas e rende moedas todo dia.", 60000, 6000, 10),
            NewAsset("terreno", "Terreno da Ilha", "🏞️", "Alugado por turistas ricos. Rende diariamente.", 150000, 15000, 11),
            NewAsset("empresa", "Empresa do Murdoc", "🏢", "A maior corporação da Ilha. Lucro diário alto.", 500000, 50000, 12),

            NewPlaceholder("segredo", "??? Ídolo Secreto", "🗿", "Algo lendário está para chegar...", 13),
            NewPlaceholder("bau", "??? Baú do Oceano", "🗝️", "Abaixo das ondas da Ilha...", 14),
            NewPlaceholder("evento", "??? Item de Evento", "🎆", "Reservado para um evento especial...", 15),

            NewRelic("relogio", "Relógio do Cassino", "⌚", "+10% em TODOS os ganhos de cassino.", 100000, RelicEffect.GainBonus, RelicGameType.All, 10, 16),
            NewRelic("relogio_slot", "Relógio da Sorte", "🎰", "+25% nos ganhos da caça-níquel.", 120000, RelicEffect.GainBonus, RelicGameType.Slots, 25, 17),
            NewRelic("relogio_roleta", "Relógio Vermelho", "🔴", "+20% nos ganhos da roleta.", 150000, RelicEffect.GainBonus, RelicGameType.Roulette, 20, 18),
            NewRelic("relogio_cash", "Relógio do Reembolso", "💸", "Devolve 15% da aposta quando você perde.", 180000, RelicEffect.Cashback, RelicGameType.All, 15, 19),
            NewRelic("relogio_dados", "Relógio da Sorte dos Dados", "🎲", "+20% nos ganhos dos dados.", 90000, RelicEffect.GainBonus, RelicGameType.Dice, 20, 20),
            NewRelic("relogio_cara", "Relógio das Duas Faces", "🪙", "+20% nos ganhos de cara ou coroa.", 70000, RelicEffect.GainBonus, RelicGameType.Coin, 20, 21),
            NewRelic("relogio_aviao", "Relógio da Decolagem", "✈️", "+20% nos ganhos do aviaozinho.", 110000, RelicEffect.GainBonus, RelicGameType.Aviao, 20, 22),
            NewRelic("relogio_poker", "Relógio do Ás", "🃏", "+20% nos ganhos do poker de máquina.", 130000, RelicEffect.GainBonus, RelicGameType.VideoPoker, 20, 23),
            NewRelic("relogio_minas", "Relógio do Mineiro", "⛏️", "+20% nos ganhos das minas.", 100000, RelicEffect.GainBonus, RelicGameType.Mines, 20, 24),
            NewRelic("relogio_limbo", "Relógio da Sorte", "🔮", "+20% nos ganhos do limbo.", 90000, RelicEffect.GainBonus, RelicGameType.Limbo, 20, 25),
            NewRelic("relogio_jokenpo", "Relógio do Desafiante", "🤚", "+20% nos ganhos do jokenpô.", 75000, RelicEffect.GainBonus, RelicGameType.Rps, 20, 26),
            NewRelic("relogio_corrida", "Relógio do Vencedor", "🏆", "+20% nos ganhos da corrida.", 115000, RelicEffect.GainBonus, RelicGameType.Race, 20, 27),
            NewRelic("relogio_plinko", "Relógio da Gravidade", "🎱", "+20% nos ganhos do plinko.", 105000, RelicEffect.GainBonus, RelicGameType.Plinko, 20, 28),
            NewRelic("relogio_roda", "Relógio da Fortuna", "🎡", "+20% nos ganhos da roda da fortuna.", 90000, RelicEffect.GainBonus, RelicGameType.Wheel, 20, 29),
            NewRelic("relogio_altobaixo", "Relógio das Cartas", "🃏", "+20% nos ganhos do maior/menor.", 85000, RelicEffect.GainBonus, RelicGameType.HighLow, 20, 30),
            NewRelic("relogio_bacara", "Relógio do Croupier", "🎴", "+20% nos ganhos do baccarat.", 140000, RelicEffect.GainBonus, RelicGameType.Baccarat, 20, 31),

            NewPet("pet_macaco", "Macaco-Caçador", "🐒", "+2% no daily por nível. Máximo de 2 tipos de pets.", 15000, UpgradeEffect.Daily, 2, 10, 32),
            NewPet("pet_gorila", "Gorila-Guarda-Costas", "🦍", "+3% no trabalho por nível. Máximo de 2 tipos de pets.", 40000, UpgradeEffect.Work, 3, 10, 33),

            NewVehicle("moto", "Moto Street", "🏍️", "Ágil nas vias da Ilha. Exige licença A.", 15000, 34, VehicleType.Moto),
            NewVehicle("carro_popular", "Carro Popular", "🚗", "O veículo do macaco assalariado. Exige licença B.", 40000, 35, VehicleType.Carro),
            NewVehicle("caminhonete", "Caminhonete 4x4", "🚙", "Toda-terreno para o trabalho pesado. Exige licença B.", 80000, 36, VehicleType.Caminhonete),
            NewVehicle("carro_esportivo", "Carro Esportivo", "🏎️", "Rápido e chamativo. Exige licença B.", 150000, 37, VehicleType.Esportivo),
            NewVehicle("caminhao", "Caminhão Basculante", "🚛", "Carrega bananas em escala industrial. Exige licença C.", 200000, 38, VehicleType.Caminhao),
            NewVehicle("onibus", "Ônibus Urbano", "🚌", "Transporte público da Ilha. Exige licença D.", 250000, 39, VehicleType.Onibus),
            NewVehicle("carreta", "Carreta de Carga", "🚚", "Para comboios de longa distância. Exige licença E.", 350000, 40, VehicleType.Carreta),
            NewVehicle("lancha", "Lancha Gorillaz", "🚤", "Para pescar no litoral. Exige licença Arrais-Amador.", 200000, 41, VehicleType.Lancha),
            NewVehicle("iate", "Iate do Murdoc", "🛥️", "Luxo nas águas da Ilha. Exige licença Mestre-Amador.", 600000, 42, VehicleType.Iate),
            NewVehicle("navio", "Navio Pirata", "🚢", "Domine os mares. Exige licença Capitão-Amador.", 2000000, 43, VehicleType.Navio),
            NewVehicle("aviao", "Avião Particular", "✈️", "Cruze os céus com estilo. Exige licença Piloto Privado.", 800000, 44, VehicleType.Aviao),
            NewVehicle("jato", "Jato Executivo", "🛩️", "O ápice da aviação. Exige licença Piloto de Linha Aérea.", 5000000, 45, VehicleType.Jato),

            NewUpgrade("upgrade_vagas_1", "Ampliação Simples", "🅿️", "+2 vagas no estacionamento particular por unidade.", 12000, UpgradeEffect.ManobristaVagas, 2, 3, 46),
            NewUpgrade("upgrade_vagas_2", "Ampliação Premium", "🅿️", "+5 vagas no estacionamento particular por unidade.", 40000, UpgradeEffect.ManobristaVagas, 5, 2, 47),
            NewUpgrade("upgrade_base_1", "Manutenção Leve", "🔧", "+10% no valor por carro estacionado por unidade.", 15000, UpgradeEffect.ManobristaBase, 10, 3, 48),
            NewUpgrade("upgrade_base_2", "Centro de Logística", "🔧", "+20% no valor por carro estacionado por unidade.", 45000, UpgradeEffect.ManobristaBase, 20, 2, 49),

            NewPet("pet_batedor", "Macaco-Batedor", "🥷", "+3% nas moedas roubadas por nível. Máximo de 2 tipos de pets.", 25000, UpgradeEffect.Rob, 3, 10, 50),

            NewWeapon("canivete", "Canivete Butterfly", "🔪", "Arma branca ágil. +8% de sucesso em assaltos.", 2500, WeaponType.Branca, 8, 5, 2000, 51),
            NewWeapon("taco", "Taco de Beisebol", "🏏", "Arma de impacto. +12% de sucesso em assaltos e defesa.", 5000, WeaponType.Branca, 12, 10, 4000, 52),
            NewWeapon("taser", "Taser de Choque", "⚡", "Defesa elétrica. +15% de sucesso; 30% chance de paralisar assaltantes.", 9000, WeaponType.Choque, 15, 30, 6000, 53),
            NewWeapon("pistola", "Pistola 9mm", "🔫", "Arma de fogo portátil. +22% de sucesso em assaltos e defesa armada.", 25000, WeaponType.Fogo, 22, 25, 15000, 54),
            NewWeapon("escopeta", "Escopeta Calibre 12", "💥", "Alto poder de fogo. +32% de sucesso e defesa.", 60000, WeaponType.Fogo, 32, 35, 35000, 55),
            NewWeapon("fuzil", "Fuzil Tático", "💣", "Armamento militar pesado. +45% de sucesso em grandes assaltos.", 140000, WeaponType.Pesada, 45, 45, 80000, 56),

            NewEquipment("luvas", "Luvas de Pelica", "🧤", "Discrição máxima. +20% de sucesso em furtos sem cooldown.", 4000, EquipmentType.Luvas, 20, 0, 57),
            NewEquipment("balaclava", "Máscara Balaclava", "🎭", "Anonimato. Oculta sua identidade caso seja pego ou cometa crimes.", 6500, EquipmentType.Mascara, 0, 0, 58),
            NewEquipment("lockpick", "Kit de Gazuas", "🗝️", "Ferramenta para arrombamento silencioso. +15% de sucesso em invasões.", 3500, EquipmentType.Lockpick, 15, 0, 59),
            NewEquipment("pedecabra", "Pé de Cabra", "🦯", "Alavanca de aço. +25% de moedas obtidas em arrombamentos.", 8000, EquipmentType.PeDeCabra, 25, 0, 60),
            NewEquipment("colete", "Colete Kevlar", "🦺", "Proteção pessoal. Reduz o dinheiro que conseguem roubar de você pela metade.", 20000, EquipmentType.Colete, 0, 50, 61),
            NewEquipment("alarme", "Alarme Residencial", "🚨", "Dispara no flagrante: dobra a multa que o invasor paga a você.", 15000, EquipmentType.Alarme, 0, 30, 62),

            NewWeapon("machadinha", "Machadinha Tática", "🪓", "Arma branca silenciosa. +14% em assaltos.", 7500, WeaponType.Branca, 14, 8, 5000, 63),
            NewWeapon("pistola_silenciosa", "Pistola Silenciada", "🎯", "Disparo sem ruído. +24% sucesso, reduz suspeita.", 38000, WeaponType.Fogo, 24, 15, 18000, 64),
            NewWeapon("besta", "Besta Tática", "🏹", "Perfuração de blindagem. +28% de sucesso em assaltos.", 48000, WeaponType.Branca, 28, 20, 25000, 65),
            NewWeapon("mp5", "Submetralhadora MP5", "🔫", "Rajada silenciada. +36% sucesso, alto poder de fogo.", 85000, WeaponType.Fogo, 36, 28, 45000, 66),
            NewWeapon("motosserra", "Motosserra do Murdoc", "🪚", "Intimidação brutal da banda. +20% sucesso.", 30000, WeaponType.Pesada, 20, 10, 12000, 67),
            NewWeapon("gl", "Lança-Granadas", "💥", "Poder destrutivo massivo. +48% sucesso, intimidação pesada.", 180000, WeaponType.Pesada, 48, 40, 100000, 68),
            NewWeapon("sniper", "Fuzil Sniper", "🎯", "Tiro de longa distância. +52% de sucesso no assalto.", 250000, WeaponType.Pesada, 52, 30, 120000, 69),
            NewWeapon("minigun", "Minigun Dourada", "👑", "Armamento lendário da Ilha. +60% sucesso e defesa máxima.", 1000000, WeaponType.Pesada, 60, 60, 300000, 70),
            NewWeapon("spray", "Spray de Pimenta", "🌶️", "Autodefesa civil. 40% de chance de afugentar ladrões.", 3500, WeaponType.Choque, 5, 20, 1000, 71),
            NewWeapon("cassetete", "Cassetete de Titânio", "🦯", "Impacto defensivo rápido. +18% de defesa armada.", 6000, WeaponType.Branca, 10, 18, 2500, 72),
            NewWeapon("taser_x2", "Taser Duplo X2", "⚡", "Paralisia elétrica dupla. 40% de defesa contra invasores.", 18000, WeaponType.Choque, 16, 40, 7500, 73),

            NewEquipment("emp", "Inibidor EMP", "📡", "Dispositivo eletrônico: anula alarmes residenciais no assalto.", 22000, EquipmentType.InibidorEmp, 15, 0, 74),
            NewEquipment("sapatilhas", "Sapatilhas Silenciosas", "🥷", "Passos amortecidos. +15% de sucesso em furtos sem cooldown.", 8500, EquipmentType.Sapatilhas, 15, 0, 75),
            NewEquipment("mochila", "Mochila Tática", "🎒", "Compartimentos reforçados. +50% de moedas roubadas.", 35000, EquipmentType.Mochila, 25, 0, 76),
            NewEquipment("scanner", "Scanner Policial", "📻", "Intercepta rádio da polícia. Reduz multas de flagrante.", 45000, EquipmentType.Scanner, 10, 10, 77),
            NewEquipment("fumigeno", "Granada de Fumaça", "💨", "Fumígeno tático de escape instantâneo.", 12000, EquipmentType.Fumigeno, 20, 15, 78),
            NewEquipment("camera4k", "Câmera Noturna 4K", "📹", "Vigilância perimetral. Revela invasores mascarados.", 18000, EquipmentType.Camera4k, 0, 25, 79),
            NewEquipment("cofre", "Cofre Fundo Falso", "🏦", "Esconderijo secreto. Protege 40% da carteira contra assaltos.", 50000, EquipmentType.Cofre, 0, 40, 80),
            NewEquipment("tinta", "Tinta Anti-Furto", "💣", "Armadilha química: queima e destrói 50% das moedas se te roubarem.", 14000, EquipmentType.Tinta, 0, 30, 81),
            NewEquipment("cerca", "Cerca Eletrificada", "⚡", "Defesa de perímetro: desestimula e repele invasores.", 28000, EquipmentType.Cerca, 0, 35, 82),
            NewEquipment("medkit", "Kit Médico", "🩺", "Primeiros socorros para emergências em combate.", 10000, EquipmentType.Medkit, 0, 15, 83),

            NewPet("pet_dobermann", "Dobermann de Guarda", "🐕", "+3% por nível de chance de afugentar invasores. Máx 2 pets.", 35000, UpgradeEffect.CrimeDefesa, 3, 10, 84),
            NewPet("pet_guaxinim", "Guaxinim Gatuno", "🦝", "+4% por nível nas moedas de furtos sem cooldown. Máx 2 pets.", 20000, UpgradeEffect.CrimeFurto, 4, 10, 85),
            NewPet("pet_falcao", "Falcão Vigilante", "🦅", "+2% por nível de fuga e visão de emboscadas. Máx 2 pets.", 45000, UpgradeEffect.CrimeDefesa, 2, 10, 86),
            NewPet("pet_serpente", "Serpente Venenosa", "🐍", "+3% por nível de dissuasão defensiva. Máx 2 pets.", 30000, UpgradeEffect.CrimeDefesa, 3, 10, 87),
            NewPet("pet_papagaio", "Papagaio Informante", "🦜", "+2% por nível de inteligência e vigilância. Máx 2 pets.", 16000, UpgradeEffect.CrimeFurto, 2, 10, 88),
            NewPet("pet_jacare", "Jacaré do Lago", "🐊", "+5% por nível de defesa máxima de magnata. Máx 2 pets.", 120000, UpgradeEffect.CrimeDefesa, 5, 10, 89),
        };
        return seed;
    }

    private static ShopItem NewWeapon(
        string key, string name, string emoji, string description, ulong price,
        WeaponType weaponType, int bonus, int defense, ulong maxStealBonus, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Weapon,
            MaxQuantity = 1,
            IsActive = true,
            SortOrder = sortOrder,
            WeaponType = weaponType,
            CrimeBonusPercent = bonus,
            CrimeDefensePercent = defense,
            CrimeMaxStealBonus = maxStealBonus
        };

    private static ShopItem NewEquipment(
        string key, string name, string emoji, string description, ulong price,
        EquipmentType equipType, int bonus, int defense, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Equipment,
            MaxQuantity = 1,
            IsActive = true,
            SortOrder = sortOrder,
            EquipmentType = equipType,
            CrimeBonusPercent = bonus,
            CrimeDefensePercent = defense
        };

    private static ShopItem NewRelic(
        string key, string name, string emoji, string description, ulong price,
        RelicEffect effect, RelicGameType game, int value, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Relic,
            Effect = BoostEffect.None,
            DurationHours = 0,
            MaxQuantity = 1,
            IsActive = true,
            SortOrder = sortOrder,
            RelicEffect = effect,
            RelicGame = game,
            RelicValue = value
        };

    private static ShopItem NewAsset(
        string key, string name, string emoji, string description, ulong price, ulong dailyIncome, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Asset,
            Effect = BoostEffect.None,
            DurationHours = 0,
            DailyIncome = dailyIncome,
            MaxQuantity = 0,
            IsActive = true,
            SortOrder = sortOrder
        };

    private static ShopItem NewPet(
        string key, string name, string emoji, string description, ulong price,
        UpgradeEffect effect, int valuePerLevel, int maxLevel, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Pet,
            Effect = BoostEffect.None,
            DurationHours = 0,
            DailyIncome = 0,
            MaxQuantity = maxLevel,
            IsActive = true,
            SortOrder = sortOrder,
            UpgradeEffect = effect,
            UpgradeValue = valuePerLevel
        };

    private static ShopItem NewVehicle(
        string key, string name, string emoji, string description, ulong price, int sortOrder,
        VehicleType type)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Vehicle,
            Effect = BoostEffect.None,
            DurationHours = 0,
            DailyIncome = 0,
            MaxQuantity = 1,
            IsActive = true,
            SortOrder = sortOrder,
            VehicleType = type,
            RequiredLicense = VehicleRules.LicenseForType(type)
        };

    private static ShopItem NewUpgrade(
        string key, string name, string emoji, string description, ulong price,
        UpgradeEffect effect, int value, int maxQuantity, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = ItemCategory.Upgrade,
            Effect = BoostEffect.None,
            DurationHours = 0,
            DailyIncome = 0,
            MaxQuantity = maxQuantity,
            IsActive = true,
            SortOrder = sortOrder,
            UpgradeEffect = effect,
            UpgradeValue = value
        };

    private static ShopItem NewPlaceholder(string key, string name, string emoji, string description, int sortOrder)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = 0,
            Category = ItemCategory.Cosmetic,
            Effect = BoostEffect.None,
            IsActive = true,
            IsPlaceholder = true,
            SortOrder = sortOrder
        };

    private static ShopItem NewItem(
        string key, string name, string emoji, string description, ulong price,
        ItemCategory category, int sortOrder, BoostEffect effect = BoostEffect.None, int durationHours = 0)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = emoji,
            Description = description,
            Price = price,
            Category = category,
            Effect = effect,
            DurationHours = durationHours,
            IsActive = true,
            SortOrder = sortOrder
        };
}
