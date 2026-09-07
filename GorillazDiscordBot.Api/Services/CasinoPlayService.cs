using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class CasinoPlayService
{
    private readonly IEconomyRepository _economy;

    public CasinoPlayService(IEconomyRepository economy)
    {
        _economy = economy;
    }

    public async Task<ulong> GetBalanceAsync(ulong userId, string username)
        => (await _economy.GetOrCreateAsync(userId, username)).Money;

    public async Task<(bool success, ulong balance)> DeductBetAsync(
        ulong userId, ulong amount, string username, string description)
    {
        await _economy.GetOrCreateAsync(userId, username);
        var (success, balance) = await _economy.TryDeductMoneyAsync(
            userId, amount, EconomyTransactionType.Bet, description);
        return (success, balance);
    }

    public async Task<ulong> PayOutAsync(
        ulong userId, ulong returnAmount, string username, string description)
    {
        if (returnAmount > 0)
        {
            await _economy.AddMoneyAsync(userId, returnAmount, EconomyTransactionType.Bet, description);
        }

        return await GetBalanceAsync(userId, username);
    }
}
