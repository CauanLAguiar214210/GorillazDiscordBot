using GorillazDiscordBot.Domain.Entity.Ranking;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IRankingRepository
{
    Task<List<RankingTier>> GetTiersAsync();
    Task<RankingTier?> GetActiveTierForAsync(ulong netWorth);
    Task<List<HallOfFame>> GetHallOfFameAsync();
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);
}