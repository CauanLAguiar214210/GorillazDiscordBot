using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class PetBonusProvider : IPetBonusProvider
{
    private readonly ShopService _shop;

    public PetBonusProvider(ShopService shop)
    {
        _shop = shop;
    }

    public Task<int> GetUpgradePercentAsync(ulong mainId, UpgradeEffect effect)
        => _shop.GetUpgradePercentByMainIdAsync(mainId, effect);

    public Task<IReadOnlyDictionary<ulong, int>> GetUpgradePercentByMainIdsAsync(
        IEnumerable<ulong> mainIds, UpgradeEffect effect)
        => _shop.GetUpgradePercentByMainIdsAsync(mainIds, effect);
}