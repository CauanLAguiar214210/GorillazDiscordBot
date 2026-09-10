using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IPetBonusProvider
{
    Task<int> GetUpgradePercentAsync(ulong mainId, UpgradeEffect effect);

    Task<IReadOnlyDictionary<ulong, int>> GetUpgradePercentByMainIdsAsync(
        IEnumerable<ulong> mainIds, UpgradeEffect effect);
}