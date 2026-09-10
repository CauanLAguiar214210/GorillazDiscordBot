using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class DiceSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public DiceSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("dados", "Aposta nos dados com botões")]
    public async Task DiceAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("tipo", "Tipo de aposta")] DiceBetChoice tipo)
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
            await RespondAsync("🎲 Você já tem uma mesa aberta! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta nos dados");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new DiceGame(ToBetType(tipo));
        _sessions.Add(Context.User.Id, CasinoSession.ForDice(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: DiceTableBuilder.BuildDiceTable(game, game.BetType, Context.User, balance),
            components: DiceTableBuilder.BuildDiceComponents());
    }

    [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.RollAction, true)]
    public async Task DiceRollAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Dice == null)
        {
            await FollowupAsync("🎲 Esta mesa não tem dados ativos. Use `/cassino dados` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.Dice;
        game.Roll();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento dos dados",
            RelicGameType.Dice, session.Bet);

        var resultSection = DescribeResult(returnAmount, game.BetType)
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = DiceTableBuilder.BuildDiceTable(game, game.BetType, Context.User, payout.Balance, resultSection);
            m.Components = DiceTableBuilder.BuildDiceResultComponents(Context.User.Id, session.Bet, game.BetType);
        });
    }

    [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task DiceReplayAsync(ulong ownerId, ulong bet, int type)
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
            await FollowupAsync("🎲 Você já tem uma mesa aberta! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova rodada nos dados");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new DiceGame((DiceBetType)type);
        _sessions.Add(Context.User.Id, CasinoSession.ForDice(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = DiceTableBuilder.BuildDiceTable(game, game.BetType, Context.User, balance);
            m.Components = DiceTableBuilder.BuildDiceComponents();
        });
    }

    [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.PaytableAction, true)]
    public async Task DicePaytableAsync()
        => await RespondAsync(embed: DiceTableBuilder.BuildDicePaytable(), ephemeral: true);

    [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.LeaveAction + ":*", true)]
    public async Task DiceLeaveAsync(ulong ownerId)
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
        if (expired.Dice is { } dice && !dice.HasRolled)
        {
            dice.Roll();
            await _play.PayOutAsync(
                Context.User.Id, dice.CalculateReturn(expired.Bet), Context.User.Username,
                "Dados expirados", RelicGameType.Dice, expired.Bet);
        }
    }

    private static string DescribeResult(ulong returnAmount, DiceBetType type)
        => returnAmount > 0
            ? $"🎉 **{DiceTableBuilder.DescribeBetType(type)}** — você venceu! Recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n"
            : $"😢 **{DiceTableBuilder.DescribeBetType(type)}** — você perdeu. Boa sorte na próxima!\n";

    private static DiceBetType ToBetType(DiceBetChoice choice) => choice switch
    {
        DiceBetChoice.Alta => DiceBetType.High,
        DiceBetChoice.Baixa => DiceBetType.Low,
        DiceBetChoice.Sete => DiceBetType.Seven,
        DiceBetChoice.Dupla => DiceBetType.Doubles,
        _ => DiceBetType.High
    };
    }
}

public enum DiceBetChoice
{
    [ChoiceDisplay("Alta (8–12)")]
    Alta,
    [ChoiceDisplay("Baixa (2–6)")]
    Baixa,
    [ChoiceDisplay("Sete (7)")]
    Sete,
    [ChoiceDisplay("Dupla (pares)")]
    Dupla
}