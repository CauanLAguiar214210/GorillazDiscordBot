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

        var defaults = DefaultCatalog();
        foreach (var item in defaults)
        {
            if (existingKeys.Contains(item.Key))
                continue;

            await _shop.UpsertAsync(item);
        }

        ForceReload();
    }

    public async Task<(bool success, string? message, ulong balance)> BuyAsync(
        ulong userId, string username, ShopItem item)
    {
        if (item.IsPlaceholder)
            return (false, $"🔒 **{item.Name}** ainda não está disponível. Volte em breve!", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);
        await _economy.GetOrCreateAsync(mainId, username);

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
                : "🎨 Este item é cosmético e não pode ser usado.", 0);

        var mainId = await _accessor.ResolveMainIdAsync(userId);

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
