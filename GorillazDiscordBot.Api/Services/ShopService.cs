using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class ShopService
{
    public static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(10);
    public const double SellRefundRate = 0.5;
    public const int MaxIncomeBacklogDays = 3;

    private readonly IShopRepository _shop;
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;

    private readonly object _lock = new();
    private List<ShopItem>? _cached;
    private DateTime _cacheExpiresAt;

    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _userLocks = new();

    private SemaphoreSlim GetUserLock(ulong mainId)
        => _userLocks.GetOrAdd(mainId, static _ => new SemaphoreSlim(1, 1));

    public ShopService(IShopRepository shop, IEconomyRepository economy, IEconomyAccessor accessor)
    {
        _shop = shop;
        _economy = economy;
        _accessor = accessor;
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

    public async Task<ulong> GetBalanceAsync(ulong userId, string username)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _economy.GetOrCreateAsync(mainId, username);
        return profile.Money;
    }

    public async Task<(ulong totalIncome, int daysCollected, int itemsCollected)> ApplyAssetIncomesAsync(
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
            return (0, 0, 0);

        var inventory = await _shop.GetInventoryAsync(mainId);
        var now = DateTime.UtcNow;
        var assetPetBonus = await GetUpgradePercentCoreAsync(mainId, UpgradeEffect.AssetIncome);
        ulong total = 0;
        int daysTotal = 0;
        int items = 0;

        foreach (var entry in inventory)
        {
            if (!assetKeys.Contains(entry.ItemKey)) continue;
            var item = catalog.First(i => i.Key == entry.ItemKey);

            var baseline = entry.LastCollectedAt ?? entry.AcquiredAt ?? now;
            var days = (int)Math.Floor((now - baseline).TotalDays);
            if (days <= 0) continue;

            days = Math.Min(days, MaxIncomeBacklogDays);
            var income = item.DailyIncome * (ulong)days;
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
                items++;
            }
        }

        return (total, daysTotal, items);
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
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, "❌ Você não possui esse item para vender.", 0);

        var refund = (ulong)Math.Floor(item.Price * SellRefundRate);
        await _economy.AddMoneyAsync(mainId, refund, EconomyTransactionType.Sell,
            $"Vendeu {item.Name}");
        await _shop.DecrementOrRemoveInventoryAsync(mainId, item.Key);

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

    public async Task<(bool success, string? message)> EquipAsync(ulong userId, string username, string keyOrName)
    {
        var item = await FindItemAsync(keyOrName);
        if (item is not { Category: ItemCategory.Relic })
            return (false, "❌ Esse item não é um relógio equipável. Use `usar` para boosts ou `comprar` para adquirir.");

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var owned = await _shop.GetInventoryByKeyAsync(mainId, item.Key);
        if (owned == null || owned.Quantity < 1)
            return (false, $"❌ Você não possui **{item.Emoji} {item.Name}** para equipar.");

        await _shop.UnequipAllAsync(mainId);
        await _shop.SetEquippedAsync(mainId, item.Key, true);
        return (true, $"⌚ **{item.Name}** equipado! Bônus ativo no cassino.");
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
        return (true, $"⌚ **{item.Name}** desequipado.");
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

    public async Task<int> GetUpgradePercentAsync(ulong userId, UpgradeEffect effect)
        => await GetUpgradePercentCoreAsync(await _accessor.ResolveMainIdAsync(userId), effect);

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
        };
        return seed;
    }

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
            MaxQuantity = 1,
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
