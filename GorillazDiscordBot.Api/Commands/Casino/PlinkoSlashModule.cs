using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

[DontAutoRegister]
[Group("cassino", "Jogos de cassino")]
public class PlinkoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public PlinkoSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("plinko", "Solte a bolinha e veja onde ela cai")]
    public async Task PlinkoAsync(
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
            await RespondAsync("🎱 Você já tem um plinko em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no plinko");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new PlinkoGame();
        _sessions.Add(Context.User.Id, CasinoSession.ForPlinko(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: PlinkoTableBuilder.BuildPlinkoTable(game, amount, Context.User, balance),
            components: PlinkoTableBuilder.BuildPlinkoComponents());
    }

    [ComponentInteraction(PlinkoTableBuilder.CustomIdPrefix + PlinkoTableBuilder.DropAction, true)]
    public async Task PlinkoDropAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Plinko == null)
        {
            await FollowupAsync("🎱 Este plinko não está mais ativo. Use `/cassino plinko` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Plinko;
        game.Drop();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado do plinko",
            RelicGameType.Plinko, session.Bet);

        var multiplier = PlinkoTableBuilder.FormatMultiplier(PlinkoGame.Multiplier(game.ResultBin!.Value));

        var resultSection = returnAmount > 0
            ? $"🎱 A bolinha caiu na faixa **{game.ResultBin.Value + 1}** "
                + $"e você ganhou **{EconomyFormat.Full(returnAmount)}** moedas ({multiplier})!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout)
            : $"❌ A bolinha caiu na faixa **{game.ResultBin.Value + 1}** "
                + $"e você perdeu **{EconomyFormat.Full(session.Bet)}** moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = PlinkoTableBuilder.BuildPlinkoTable(
                game, session.Bet, Context.User, payout.Balance, resultSection);
            m.Components = PlinkoTableBuilder.BuildPlinkoResultComponents(
                Context.User.Id, session.Bet);
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

        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await FollowupAsync("🎱 Você já tem um plinko em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Novo plinko");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new PlinkoGame();
        _sessions.Add(Context.User.Id, CasinoSession.ForPlinko(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = PlinkoTableBuilder.BuildPlinkoTable(game, bet, Context.User, balance);
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

        _sessions.Remove(Context.User.Id);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task SettleExpiredAsync(CasinoSession expired)
    {
        if (expired.Plinko is { } plinko && !plinko.HasDropped)
        {
            plinko.Drop();
            await _play.PayOutAsync(
                Context.User.Id, plinko.CalculateReturn(expired.Bet), Context.User.Username, "Plinko expirado",
                RelicGameType.Plinko, expired.Bet);
        }
    }
}