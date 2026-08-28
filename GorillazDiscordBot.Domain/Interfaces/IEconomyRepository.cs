using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IEconomyRepository
{
    Task<EconomyProfile> GetOrCreateAsync(ulong userId, string username);

    Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId, int reward);

    Task<bool> AddMoneyAsync(ulong userId, int amount, EconomyTransactionType type, string description);
    Task<(bool success, int newBalance)> TryDeductMoneyAsync(ulong userId, int amount, EconomyTransactionType type, string description);

    Task<(bool success, int wallet, int bank)> DepositAsync(ulong userId, int amount);
    Task<(bool success, int wallet, int bank)> WithdrawAsync(ulong userId, int amount);

    Task<(bool success, int wallet, int savings, int streak)> DepositSavingsAsync(ulong userId, int amount);
    Task<(bool success, int wallet, int savings, int streak)> WithdrawSavingsAsync(ulong userId, int amount);

    Task<EconomyProfile> SetLastWorkAsync(ulong userId, DateTime now);
    Task<EconomyProfile> SetRobAttemptAsync(ulong userId, DateTime attemptTime, DateTime? caughtUntil);

    Task<List<EconomyProfile>> GetTopUsersAsync(int limit);
    Task<int> ApplyDailyMaintenanceAsync();
    Task<List<EconomyTransaction>> GetHistoryAsync(ulong userId, int limit);
}