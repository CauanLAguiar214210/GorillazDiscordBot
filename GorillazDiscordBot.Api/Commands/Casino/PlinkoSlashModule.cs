using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;

namespace GorillazDiscordBot.Commands.Casino;

[DontAutoRegister]
public class PlinkoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoApiClient _casino;
    private readonly PayoutService _play;
    private readonly CasinoBetTracker _bets;

    public PlinkoSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
    {
        _casino = casino;
        _play = play;
        _bets = bets;
    }

    [SlashCommand("plinko", "Solte a bolinha e veja onde ela cai")]
    public async Task PlinkoAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
    {
        if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
        {
            await RespondAsync(parseError, ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Plinko, Context.User.Id, amount, null,
            "🎱 Você já tem um plinko em andamento! Use os botões do jogo aberto.");
        if (opened == null)
            return;

        if (opened.ExpiredSettle is { } expired)
            await SettleExpiredAsync(expired);

        if (!await DeductOrLeaveAsync(amount, "Aposta no plinko"))
            return;

        _bets.Set(Context.User.Id, amount);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        await RespondAsync(
            embed: PlinkoTableBuilder.BuildPlinkoTable(opened.State.Plinko!, amount, Context.User, balance),
            components: PlinkoTableBuilder.BuildPlinkoComponents());
    }

    [ComponentInteraction(PlinkoTableBuilder.CustomIdPrefix + PlinkoTableBuilder.DropAction, true)]
    public async Task PlinkoDropAsync()
    {
        await DeferAsync();

        var done = await CasinoApiFlow.RunActionAsync(
            this, _casino, GameKind.Plinko, Context.User.Id, "drop", null,
            "🎱 Este plinko não está mais ativo. Use `/cassino plinko` para começar outro.");
        if (done == null)
            return;

        var bet = _bets.Get(Context.User.Id);
        var returnAmount = done.Outcome?.ReturnAmount ?? 0;
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado do plinko",
            RelicGameType.Plinko, bet);
        _bets.Remove(Context.User.Id);

        var state = done.State.Plinko!;
        var resultSection = (returnAmount > 0
                ? $"🎱 A bolinha caiu na faixa **{state.ResultBin!.Value + 1}** "
                    + $"e você ganhou **{EconomyFormat.Full(returnAmount)}** moedas "
                    + $"({PlinkoTableBuilder.FormatMultiplier(state.Multiplier)})!\n"
                : $"❌ A bolinha caiu na faixa **{state.ResultBin!.Value + 1}** "
                    + $"e você perdeu **{EconomyFormat.Full(bet)}** moedas.\n")
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = PlinkoTableBuilder.BuildPlinkoTable(state, bet, Context.User, payout.Balance, resultSection);
            m.Components = PlinkoTableBuilder.BuildPlinkoResultComponents(Context.User.Id, bet);
        });
    }

    [ComponentInteraction(PlinkoTableBuilder.CustomIdPrefix + PlinkoTableBuilder.ReplayAction + ":*:*", true)]
    public async Task PlinkoReplayAsync(ulong ownerId, ulong bet)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Plinko, Context.User.Id, bet, null,
            "🎱 Você já tem um plinko em andamento! Use os botões do jogo aberto.", followup: true);
        if (opened == null)
            return;

        if (opened.ExpiredSettle is { } expired)
            await SettleExpiredAsync(expired);

        if (!await DeductOrLeaveAsync(bet, "Novo plinko", followup: true))
            return;

        _bets.Set(Context.User.Id, bet);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = PlinkoTableBuilder.BuildPlinkoTable(opened.State.Plinko!, bet, Context.User, balance);
            m.Components = PlinkoTableBuilder.BuildPlinkoComponents();
        });
    }

    [ComponentInteraction(PlinkoTableBuilder.CustomIdPrefix + PlinkoTableBuilder.PaytableAction, true)]
    public async Task PlinkoPaytableAsync()
        => await RespondAsync(embed: PlinkoTableBuilder.BuildPlinkoPaytable(), ephemeral: true);

    [ComponentInteraction(PlinkoTableBuilder.CustomIdPrefix + PlinkoTableBuilder.LeaveAction + ":*", true)]
    public async Task PlinkoLeaveAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Plinko, Context.User.Id);
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
            "Plinko expirado", RelicGameType.Plinko, _bets.Get(Context.User.Id));
    }

    private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
    {
        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, description);

        if (deducted)
            return true;

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Plinko, Context.User.Id);

        var message = "❌ Você não tem moedas suficientes na carteira.";
        if (followup)
            await FollowupAsync(message, ephemeral: true);
        else
            await RespondAsync(message, ephemeral: true);

        return false;
    }
}