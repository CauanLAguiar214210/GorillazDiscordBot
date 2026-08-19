using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IUserRepository
{
    Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username);
    Task<(bool claimed, int newBalance)> TryClaimDailyAsync(ulong userId);
    Task<bool> AddMoneyAsync(ulong userId, int amount);
    Task<(bool success, int wallet, int bank)> DepositAsync(ulong userId, int amount);
    Task<(bool success, int wallet, int bank)> WithdrawAsync(ulong userId, int amount);
    Task<List<DiscordUserProfile>> GetTopUsersAsync(int limit);
    Task<int> ApplyBankTaxAsync();
}
