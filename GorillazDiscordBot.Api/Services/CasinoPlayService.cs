using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class CasinoPlayService
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;

    public CasinoPlayService(IEconomyRepository economy, IEconomyAccessor accessor)
    {
        _economy = economy;
        _accessor = accessor;
    }

    public async Task<ulong> GetBalanceAsync(ulong userId, string username)
        => (await _economy.GetOrCreateAsync(await _accessor.ResolveMainIdAsync(userId), username)).Money;

    public async Task<(bool success, ulong balance)> DeductBetAsync(
        ulong userId, ulong amount, string username, string description)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);
        await _economy.GetOrCreateAsync(mainId, username);
        var (success, balance) = await _economy.TryDeductMoneyAsync(
            mainId, amount, EconomyTransactionType.Bet, description);
        return (success, balance);
    }

    public async Task<ulong> PayOutAsync(
        ulong userId, ulong returnAmount, string username, string description)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);

        if (returnAmount > 0)
        {
            await _economy.AddMoneyAsync(mainId, returnAmount, EconomyTransactionType.Bet, description);
        }

        return await GetBalanceAsync(userId, username);
    }
}
