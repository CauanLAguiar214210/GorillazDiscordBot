using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Ranking;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class PatrimonioService : IPatrimonioService
{
    private readonly IEconomyRepository _economy;
    private readonly ShopService _shop;
    private readonly IRankingRepository _ranking;
    private readonly IEconomyAccessor _accessor;
    private readonly IUserRepository _users;

    public PatrimonioService(
        IEconomyRepository economy,
        ShopService shop,
        IRankingRepository ranking,
        IEconomyAccessor accessor,
        IUserRepository users)
    {
        _economy = economy;
        _shop = shop;
        _ranking = ranking;
        _accessor = accessor;
        _users = users;
    }

    public async Task<PatrimonioSnapshot> GetSnapshotAsync(ulong userId, string? username = null)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        var profile = await _economy.GetOrCreateAsync(mainId, username ?? string.Empty);
        return await ComputeSnapshotAsync(mainId, profile);
    }

    private async Task<PatrimonioSnapshot> ComputeSnapshotAsync(ulong mainId, EconomyProfile profile)
    {
        var cash = profile.NetWorth;
        var items = await _shop.GetInventoryValueAsync(mainId);
        var total = CheckedAdd(cash, items);
        return new PatrimonioSnapshot(profile.Money, profile.Bank, profile.Savings, cash, items, total);
    }

    public async Task<HallOfFame?> GetHallOfFameAsync(ulong userId)
    {
        var group = await _users.GetGroupAsync(userId);
        var ids = group.Select(g => g.UserId).Distinct().ToList();
        return await _ranking.GetHallOfFameForMembersAsync(ids);
    }

    public async Task<List<WealthRankingEntry>> GetTopSnapshotsAsync(int limit)
    {
        var bufferSize = Math.Max(limit, 5) + 20;
        var buffer = await _economy.GetTopUsersAsync(bufferSize);

        var ranked = new List<(ulong Total, WealthRankingEntry Entry)>(buffer.Count);
        foreach (var profile in buffer)
        {
            var snapshot = await ComputeSnapshotAsync(profile.UserId, profile);

            if(snapshot.Total < 1000000000000000)
                ranked.Add((snapshot.Total, new WealthRankingEntry(profile.UserId, profile.Username, snapshot)));
        }

        return ranked
            .OrderByDescending(s => s.Total)
            .ThenByDescending(s => s.Entry.Snapshot.Money)
            .Select(s => s.Entry)
            .Take(limit)
            .ToList();
    }

    private static ulong CheckedAdd(ulong a, ulong b)
    {
        var sum = a + b;
        return sum < a ? ulong.MaxValue : sum;
    }
}