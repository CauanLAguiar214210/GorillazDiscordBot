using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class HighLowSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public HighLowSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("altobaixo", "Acerte se a próxima carta é maior ou menor")]
    public async Task HighLowAsync(
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
            await RespondAsync("🃏 Você já tem uma rodada em andamento! Use os botões da rodada aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no maior/menor");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new HighLowGame(amount);
        _sessions.Add(Context.User.Id, CasinoSession.ForHighLow(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: HighLowTableBuilder.BuildHighLowTable(game, Context.User, balance),
            components: HighLowTableBuilder.BuildHighLowComponents(game, Context.User.Id));
    }

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.HigherAction, true)]
    public async Task HighLowHigherAsync()
        => await GuessAsync(true);

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.LowerAction, true)]
    public async Task HighLowLowerAsync()
        => await GuessAsync(false);

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.CashOutAction, true)]
    public async Task HighLowCashOutAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.HighLow == null)
        {
            await FollowupAsync("🃏 Esta rodada não está mais ativa. Use `/cassino altobaixo` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.HighLow;
        var returnAmount = game.CashOut();
        _sessions.Remove(Context.User.Id);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento do maior/menor",
            RelicGameType.HighLow, session.Bet);

        var resultSection = $"🪂 Você saiu em **{HighLowTableBuilder.FormatMultiplier(game.Multiplier)}** e resgatou "
            + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = HighLowTableBuilder.BuildHighLowTable(game, Context.User, payout.Balance, resultSection);
            m.Components = HighLowTableBuilder.BuildHighLowResultComponents(Context.User.Id, session.Bet);
        });
    }

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.ReplayAction + ":*:*", true)]
    public async Task HighLowReplayAsync(ulong ownerId, ulong bet)
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
            await FollowupAsync("🃏 Você já tem uma rodada em andamento! Use os botões da rodada aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova rodada de maior/menor");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new HighLowGame(bet);
        _sessions.Add(Context.User.Id, CasinoSession.ForHighLow(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = HighLowTableBuilder.BuildHighLowTable(game, Context.User, balance);
            m.Components = HighLowTableBuilder.BuildHighLowComponents(game, Context.User.Id);
        });
    }

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.PaytableAction, true)]
    public async Task HighLowPaytableAsync()
        => await RespondAsync(embed: HighLowTableBuilder.BuildHighLowPaytable(), ephemeral: true);

    [ComponentInteraction(HighLowTableBuilder.CustomIdPrefix + HighLowTableBuilder.LeaveAction + ":*", true)]
    public async Task HighLowLeaveAsync(ulong ownerId)
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

    private async Task GuessAsync(bool higher)
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.HighLow == null)
        {
            await FollowupAsync("🃏 Esta rodada não está mais ativa. Use `/cassino altobaixo` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.HighLow;
        var previousCard = game.CurrentCard;
        var result = game.Guess(higher);

        if (result == HighLowGuessResult.Lose)
        {
            _sessions.Remove(Context.User.Id);

            var payout = await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Errou no maior/menor",
                RelicGameType.HighLow, session.Bet);

            var isImpossiblePick = game.CurrentCard.Symbol == previousCard.Symbol;
            var resultSection = (isImpossiblePick
                    ? $"❌ Jogada impossível com **`{previousCard.Symbol}`** — você perdeu na hora! Perdeu "
                    : $"❌ A carta era **`{previousCard.Symbol}`** e a próxima foi **`{game.CurrentCard.Symbol}`** — você errou! Perdeu ")
                + $"{EconomyFormat.Full(session.Bet)} moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = HighLowTableBuilder.BuildHighLowTable(game, Context.User, payout.Balance, resultSection);
                m.Components = HighLowTableBuilder.BuildHighLowResultComponents(Context.User.Id, session.Bet);
            });
            return;
        }

        _sessions.Touch(Context.User.Id);
        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        var section = result == HighLowGuessResult.Tie
            ? $"🤝 Empate! **`{previousCard.Symbol}`** → **`{game.CurrentCard.Symbol}`** — carta trocada."
            : $"✅ Acertou! **`{previousCard.Symbol}`** → **`{game.CurrentCard.Symbol}`** — carta atual!";

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = HighLowTableBuilder.BuildHighLowTable(game, Context.User, balance, section);
            m.Components = HighLowTableBuilder.BuildHighLowComponents(game, Context.User.Id);
        });
    }

    private async Task SettleExpiredAsync(CasinoSession expired)
    {
        if (expired.HighLow is { } highLow && !highLow.IsFinished)
        {
            await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Maior/menor expirado",
                RelicGameType.HighLow, expired.Bet);
        }
    }
    }
}