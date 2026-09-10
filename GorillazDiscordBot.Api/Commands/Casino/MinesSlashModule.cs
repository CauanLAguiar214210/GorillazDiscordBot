using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class MinesSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public MinesSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("minas", "Revela células seguras antes de achar uma mina")]
    public async Task MinesAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("minas", "Quantidade de minas na grade")] MinesCountChoice minas = MinesCountChoice.Quatro)
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
            await RespondAsync("🧨 Você já tem um campo aberto! Use os botões do campo existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta nas minas");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new MinesGame(amount, ToMinesCount(minas));
        _sessions.Add(Context.User.Id, CasinoSession.ForMines(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: MinesTableBuilder.BuildMinesTable(game, Context.User, balance),
            components: MinesTableBuilder.BuildMinesComponents(game, Context.User.Id));
    }

    [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.RevealAction + ":*", true)]
    public async Task MinesRevealAsync(int cell)
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Mines == null)
        {
            await FollowupAsync("🧨 Este campo não está mais ativo. Use `/cassino minas` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Mines;
        var safe = game.Reveal(cell);

        if (!safe)
        {
            _sessions.Remove(Context.User.Id);

            var payout = await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Mina encontrada",
                RelicGameType.Mines, session.Bet);

            var resultSection = "💥 **Você pisou numa mina!** Perdeu a aposta.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = MinesTableBuilder.BuildMinesTable(game, Context.User, payout.Balance, resultSection);
                m.Components = MinesTableBuilder.BuildMinesResultComponents(
                    Context.User.Id, session.Bet, game.MinesCount);
            });
            return;
        }

        _sessions.Touch(Context.User.Id);
        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = MinesTableBuilder.BuildMinesTable(game, Context.User, balance);
            m.Components = MinesTableBuilder.BuildMinesComponents(game, Context.User.Id);
        });
    }

    [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.CashOutAction, true)]
    public async Task MinesCashOutAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Mines == null)
        {
            await FollowupAsync("🧨 Este campo não está mais ativo. Use `/cassino minas` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Mines;
        var returnAmount = game.CashOut();
        _sessions.Remove(Context.User.Id);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento das minas",
            RelicGameType.Mines, session.Bet);

        var resultSection = $"🪂 Você saiu em **{MinesTableBuilder.FormatMultiplier(game.CurrentMultiplier)}** e resgatou "
            + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = MinesTableBuilder.BuildMinesTable(game, Context.User, payout.Balance, resultSection);
            m.Components = MinesTableBuilder.BuildMinesResultComponents(
                Context.User.Id, session.Bet, game.MinesCount);
        });
    }

    [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task MinesReplayAsync(ulong ownerId, ulong bet, int mines)
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
            await FollowupAsync("🧨 Você já tem um campo aberto! Use os botões do campo existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Novo campo nas minas");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new MinesGame(bet, mines);
        _sessions.Add(Context.User.Id, CasinoSession.ForMines(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = MinesTableBuilder.BuildMinesTable(game, Context.User, balance);
            m.Components = MinesTableBuilder.BuildMinesComponents(game, Context.User.Id);
        });
    }

    [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.PaytableAction, true)]
    public async Task MinesPaytableAsync()
        => await RespondAsync(embed: MinesTableBuilder.BuildMinesPaytable(), ephemeral: true);

    [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.LeaveAction + ":*", true)]
    public async Task MinesLeaveAsync(ulong ownerId)
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
        if (expired.Mines is { } mines && !mines.IsFinished)
        {
            await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Minas expiradas",
                RelicGameType.Mines, expired.Bet);
        }
    }

    private static int ToMinesCount(MinesCountChoice choice) => (int)choice + 1;
    }
}

public enum MinesCountChoice
{
    [ChoiceDisplay("1 mina")]
    Uma,
    [ChoiceDisplay("2 minas")]
    Duas,
    [ChoiceDisplay("3 minas")]
    Tres,
    [ChoiceDisplay("4 minas")]
    Quatro,
    [ChoiceDisplay("5 minas")]
    Cinco,
    [ChoiceDisplay("6 minas")]
    Seis
}