using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class RaceSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public RaceSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("corrida", "Aposta no cavalo que vai vencer a corrida")]
    public async Task RaceAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("cavalo", "O cavalo em que você quer apostar")] RacePickChoice cavalo)
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
            await RespondAsync("🏇 Você já tem uma corrida em andamento! Use os botões da corrida aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta na corrida");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new RaceGame((int)cavalo);
        _sessions.Add(Context.User.Id, CasinoSession.ForRace(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: RaceTableBuilder.BuildRaceTable(game, amount, Context.User, balance),
            components: RaceTableBuilder.BuildRaceComponents(Context.User.Id));
    }

    [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.StartAction, true)]
    public async Task RaceStartAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Race == null)
        {
            await FollowupAsync("🏇 Esta corrida não está mais ativa. Use `/cassino corrida` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.Race;
        game.Start();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado da corrida",
            RelicGameType.Race, session.Bet);

        var resultSection = game.IsWin
            ? $"🏆 Seu **{RaceTableBuilder.DescribePick(game.WinnerIndex!.Value)}** venceu e você faturou "
                + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout)
            : $"❌ O vencedor foi **{RaceTableBuilder.DescribePick(game.WinnerIndex!.Value)}**. "
                + $"Perdeu **{EconomyFormat.Full(session.Bet)}** moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = RaceTableBuilder.BuildRaceTable(
                game, session.Bet, Context.User, payout.Balance, resultSection);
            m.Components = RaceTableBuilder.BuildRaceResultComponents(
                Context.User.Id, session.Bet, (int)game.PlayerPick);
        });
    }

    [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task RaceReplayAsync(ulong ownerId, ulong bet, int pick)
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
            await FollowupAsync("🏇 Você já tem uma corrida em andamento! Use os botões da corrida aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova corrida");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new RaceGame(pick);
        _sessions.Add(Context.User.Id, CasinoSession.ForRace(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = RaceTableBuilder.BuildRaceTable(game, bet, Context.User, balance);
            m.Components = RaceTableBuilder.BuildRaceComponents(Context.User.Id);
        });
    }

    [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.PaytableAction, true)]
    public async Task RacePaytableAsync()
        => await RespondAsync(embed: RaceTableBuilder.BuildRacePaytable(), ephemeral: true);

    [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.LeaveAction + ":*", true)]
    public async Task RaceLeaveAsync(ulong ownerId)
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
        if (expired.Race is { } race && !race.HasFinished)
        {
            race.Start();
            await _play.PayOutAsync(
                Context.User.Id, race.CalculateReturn(expired.Bet), Context.User.Username, "Corrida expirada",
                RelicGameType.Race, expired.Bet);
        }
    }
    }
}

public enum RacePickChoice
{
    [ChoiceDisplay("Cavalo 1 🐎")]
    Um,
    [ChoiceDisplay("Cavalo 2 🐴")]
    Dois,
    [ChoiceDisplay("Cavalo 3 🦄")]
    Tres,
    [ChoiceDisplay("Cavalo 4 🐎")]
    Quatro,
    [ChoiceDisplay("Cavalo 5 🐴")]
    Cinco,
    [ChoiceDisplay("Cavalo 6 🦄")]
    Seis
}