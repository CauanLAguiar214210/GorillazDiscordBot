using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;

namespace GorillazDiscordBot.Commands.Casino;

[Disabled]
public class WheelSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoApiClient _casino;
    private readonly PayoutService _play;
    private readonly CasinoBetTracker _bets;

    public WheelSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
    {
        _casino = casino;
        _play = play;
        _bets = bets;
    }

    [SlashCommand("roda", "Gire a roda e veja onde ela para")]
    public async Task WheelAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
    {
        if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
        {
            await RespondAsync(parseError, ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Wheel, Context.User.Id, amount, null,
            "🎡 Você já tem uma roda em andamento! Use os botões da roda aberta.");
        if (opened == null)
            return;

        if (opened.ExpiredSettle is { } expired)
            await SettleExpiredAsync(expired);

        if (!await DeductOrLeaveAsync(amount, "Aposta na roda da fortuna"))
            return;

        _bets.Set(Context.User.Id, amount);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        await RespondAsync(
            embed: WheelTableBuilder.BuildWheelTable(opened.State.Wheel!, Context.User, balance),
            components: WheelTableBuilder.BuildWheelComponents());
    }

    [ComponentInteraction(WheelTableBuilder.CustomIdPrefix + WheelTableBuilder.SpinAction, true)]
    public async Task WheelSpinAsync()
    {
        await DeferAsync();

        var done = await CasinoApiFlow.RunActionAsync(
            this, _casino, GameKind.Wheel, Context.User.Id, "spin", null,
            "🎡 Esta roda não está mais ativa. Use `/cassino roda` para girar outra.");
        if (done == null)
            return;

        var bet = _bets.Get(Context.User.Id);
        var payout = await _play.PayOutAsync(
            Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
            "Resultado da roda da fortuna", RelicGameType.Wheel, bet);
        _bets.Remove(Context.User.Id);

        var state = done.State.Wheel!;
        var resultSection = (state.ResultMultiplier >= 1.0
                ? $"🎉 A roda parou em **{WheelTableBuilder.FormatMultiplier(state.ResultMultiplier)}** e você faturou "
                    + $"**{EconomyFormat.Full(done.Outcome?.ReturnAmount ?? 0)}** moedas!\n"
                : $"😢 A roda parou em **{WheelTableBuilder.FormatMultiplier(state.ResultMultiplier)}** e você recebeu apenas "
                    + $"**{EconomyFormat.Full(done.Outcome?.ReturnAmount ?? 0)}** moedas.\n")
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = WheelTableBuilder.BuildWheelTable(state, Context.User, payout.Balance, resultSection);
            m.Components = WheelTableBuilder.BuildWheelResultComponents(Context.User.Id, bet);
        });
    }

    [ComponentInteraction(WheelTableBuilder.CustomIdPrefix + WheelTableBuilder.ReplayAction + ":*:*", true)]
    public async Task WheelReplayAsync(ulong ownerId, ulong bet)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Wheel, Context.User.Id, bet, null,
            "🎡 Você já tem uma roda em andamento! Use os botões da roda aberta.", followup: true);
        if (opened == null)
            return;

        if (opened.ExpiredSettle is { } expired)
            await SettleExpiredAsync(expired);

        if (!await DeductOrLeaveAsync(bet, "Nova roda da fortuna", followup: true))
            return;

        _bets.Set(Context.User.Id, bet);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = WheelTableBuilder.BuildWheelTable(opened.State.Wheel!, Context.User, balance);
            m.Components = WheelTableBuilder.BuildWheelComponents();
        });
    }

    [ComponentInteraction(WheelTableBuilder.CustomIdPrefix + WheelTableBuilder.PaytableAction, true)]
    public async Task WheelPaytableAsync()
        => await RespondAsync(embed: WheelTableBuilder.BuildWheelPaytable(), ephemeral: true);

    [ComponentInteraction(WheelTableBuilder.CustomIdPrefix + WheelTableBuilder.LeaveAction + ":*", true)]
    public async Task WheelLeaveAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Wheel, Context.User.Id);
        _bets.Remove(Context.User.Id);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task SettleExpiredAsync(Outcome expired)
    {
        await _play.PayOutAsync(
            Context.User.Id, expired.ReturnAmount, Context.User.Username,
            "Roda expirada", RelicGameType.Wheel, _bets.Get(Context.User.Id));
    }

    private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
    {
        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, description);

        if (deducted)
            return true;

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Wheel, Context.User.Id);

        var message = "❌ Você não tem moedas suficientes na carteira.";
        if (followup)
            await FollowupAsync(message, ephemeral: true);
        else
            await RespondAsync(message, ephemeral: true);

        return false;
    }
}