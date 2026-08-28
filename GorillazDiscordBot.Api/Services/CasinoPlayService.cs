using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

public class CasinoPlayService
{
    private readonly IEconomyRepository _economy;

    public CasinoPlayService(IEconomyRepository economy)
    {
        _economy = economy;
    }

    public async Task<int> GetBalanceAsync(ulong userId, string username)
        => (await _economy.GetOrCreateAsync(userId, username)).Money;

    public async Task<(bool success, int balance)> DeductBetAsync(
        ulong userId, int amount, string username, string description)
    {
        await _economy.GetOrCreateAsync(userId, username);
        var (success, balance) = await _economy.TryDeductMoneyAsync(
            userId, amount, EconomyTransactionType.Bet, description);
        return (success, balance);
    }

    public async Task<int> PayOutAsync(
        ulong userId, int returnAmount, string username, string description)
    {
        if (returnAmount > 0)
        {
            await _economy.AddMoneyAsync(userId, returnAmount, EconomyTransactionType.Bet, description);
        }

        return await GetBalanceAsync(userId, username);
    }
}
