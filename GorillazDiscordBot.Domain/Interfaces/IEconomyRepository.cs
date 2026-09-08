using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IEconomyRepository
{
    Task<EconomyProfile> GetOrCreateAsync(ulong userId, string username);

    Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId, int reward);

    Task<bool> AddMoneyAsync(ulong userId, ulong amount, EconomyTransactionType type, string description);
    Task<(bool success, ulong newBalance)> TryDeductMoneyAsync(ulong userId, ulong amount, EconomyTransactionType type, string description);

    Task<(bool success, int wallet, int bank)> DepositAsync(ulong userId, ulong amount);
    Task<(bool success, int wallet, int bank)> WithdrawAsync(ulong userId, ulong amount);

    Task<(bool success, int wallet, int savings, int streak)> DepositSavingsAsync(ulong userId, ulong amount);
    Task<(bool success, int wallet, int savings, int streak)> WithdrawSavingsAsync(ulong userId, ulong amount);

    Task<EconomyProfile> SetLastWorkAsync(ulong userId, DateTime now);
    Task<EconomyProfile> SetRobAttemptAsync(ulong userId, DateTime attemptTime, DateTime? caughtUntil);

    Task<EconomyProfile> SetDailyBoostAsync(ulong userId, bool pending);
    Task<EconomyProfile> SetWorkBoostAsync(ulong userId, bool pending);
    Task<EconomyProfile> SetRobShieldAsync(ulong userId, DateTime? until);

    Task<List<EconomyProfile>> GetTopUsersAsync(int limit);
    Task<int> ApplyDailyMaintenanceAsync();
    Task<List<EconomyTransaction>> GetHistoryAsync(ulong userId, int limit);
    Task LogTransactionAsync(ulong userId, EconomyTransactionType type, long amount, string description);
    Task<UnifyResult?> UnifyProfileAsync(ulong sourceUserId, ulong targetUserId);
}