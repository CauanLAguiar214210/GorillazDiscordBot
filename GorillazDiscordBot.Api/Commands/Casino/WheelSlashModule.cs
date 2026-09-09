using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

[Disabled]
public class WheelSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public WheelSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("roda", "Gire a roda e veja onde ela para")]
    public async Task WheelAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor)
    {
        if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
        {
            await RespondAsync(parseError, ephemeral: true);
            return;
        }

        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await RespondAsync("🎡 Você já tem uma roda em andamento! Use os botões da roda aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta na roda da fortuna");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new WheelGame(amount);
        _sessions.Add(Context.User.Id, CasinoSession.ForWheel(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: WheelTableBuilder.BuildWheelTable(game, Context.User, balance),
            components: WheelTableBuilder.BuildWheelComponents());
    }

    [ComponentInteraction(WheelTableBuilder.CustomIdPrefix + WheelTableBuilder.SpinAction, true)]
    public async Task WheelSpinAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Wheel == null)
        {
            await FollowupAsync("🎡 Esta roda não está mais ativa. Use `/roda` para girar outra.", ephemeral: true);
            return;
        }

        var game = session.Wheel;
        game.Spin();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado da roda da fortuna",
            RelicGameType.Wheel, session.Bet);

        var resultSection = game.ResultMultiplier >= 1.0
            ? $"🎉 A roda parou em **{WheelTableBuilder.FormatMultiplier(game.ResultMultiplier)}** e você faturou "
                + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout)
            : $"😢 A roda parou em **{WheelTableBuilder.FormatMultiplier(game.ResultMultiplier)}** e você recebeu apenas "
                + $"**{EconomyFormat.Full(returnAmount)}** moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = WheelTableBuilder.BuildWheelTable(game, Context.User, payout.Balance, resultSection);
            m.Components = WheelTableBuilder.BuildWheelResultComponents(Context.User.Id, session.Bet);
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

        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await FollowupAsync("🎡 Você já tem uma roda em andamento! Use os botões da roda aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova roda da fortuna");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new WheelGame(bet);
        _sessions.Add(Context.User.Id, CasinoSession.ForWheel(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = WheelTableBuilder.BuildWheelTable(game, Context.User, balance);
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

        _sessions.Remove(Context.User.Id);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task SettleExpiredAsync(CasinoSession expired)
    {
        if (expired.Wheel is { } wheel && !wheel.HasSpun)
        {
            wheel.Spin();
            await _play.PayOutAsync(
                Context.User.Id, wheel.CalculateReturn(expired.Bet), Context.User.Username, "Roda expirada",
                RelicGameType.Wheel, expired.Bet);
        }
    }
}