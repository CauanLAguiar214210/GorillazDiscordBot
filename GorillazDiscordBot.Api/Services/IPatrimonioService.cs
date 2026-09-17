using GorillazDiscordBot.Domain.Entity.Ranking;

namespace GorillazDiscordBot.Services;

public sealed record PatrimonioSnapshot(
    ulong Money,
    ulong Bank,
    ulong Savings,
    ulong CashTotal,
    ulong ItemsValue,
    ulong Total);

public sealed record WealthRankingEntry(ulong UserId, string Username, PatrimonioSnapshot Snapshot);

public interface IPatrimonioService
{
    Task<PatrimonioSnapshot> GetSnapshotAsync(ulong userId, string? username = null);
    Task<HallOfFame?> GetHallOfFameAsync(ulong userId);
    Task<List<WealthRankingEntry>> GetTopSnapshotsAsync(int limit);
}