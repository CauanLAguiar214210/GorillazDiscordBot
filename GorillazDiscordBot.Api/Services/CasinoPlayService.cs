using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public class CasinoPlayService
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly ShopService _shop;

    public CasinoPlayService(IEconomyRepository economy, IEconomyAccessor accessor, ShopService shop)
    {
        _economy = economy;
        _accessor = accessor;
        _shop = shop;
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

    public async Task<PayoutResult> PayOutAsync(
        ulong userId, ulong returnAmount, string username, string description,
        RelicGameType gameType = RelicGameType.All, ulong totalBet = 0)
    {
        var mainId = await _accessor.ResolveMainIdAsync(userId);

        var effective = returnAmount;
        AppliedRelic? appliedRelic = null;
        var (relic, value, isCashback) = await _shop.GetEquippedRelicAsync(mainId);

        if (relic != null && RelicApplies(relic.RelicGame, gameType))
        {
            if (isCashback && returnAmount == 0 && totalBet > 0)
            {
                effective = totalBet * (ulong)value / 100UL;
                appliedRelic = CreateAppliedRelic(relic);
            }
            else if (!isCashback && returnAmount > 0)
            {
                effective = returnAmount + (returnAmount * (ulong)value / 100UL);
                if (effective > returnAmount)
                    appliedRelic = CreateAppliedRelic(relic);
            }
        }

        if (effective > 0)
        {
            await _economy.AddMoneyAsync(mainId, effective, EconomyTransactionType.Bet, description);
        }

        var balance = await GetBalanceAsync(userId, username);
        return new PayoutResult(balance, returnAmount, effective - returnAmount, appliedRelic);
    }

    private static AppliedRelic CreateAppliedRelic(ShopItem relic)
        => new(relic.Name, relic.Emoji, relic.RelicEffect, relic.RelicValue);

    private static bool RelicApplies(RelicGameType relicGame, RelicGameType playedGame)
        => relicGame == RelicGameType.All || relicGame == playedGame;
}