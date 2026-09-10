using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

[Group("cassino", "Jogos de cassino")]
public class CoinFlipSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public CoinFlipSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("caraoucoroa", "Aposta em cara ou coroa com botões")]
    public async Task CoinFlipAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("lado", "Lado da moeda")] CoinSideChoice lado)
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
            await RespondAsync("🪙 Você já tem uma moeda no ar! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta em cara ou coroa");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new CoinFlipGame(ToSide(lado));
        _sessions.Add(Context.User.Id, CasinoSession.ForCoin(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: CoinTableBuilder.BuildCoinFlipTable(game, game.Side, Context.User, balance),
            components: CoinTableBuilder.BuildCoinComponents());
    }

    [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.FlipAction, true)]
    public async Task CoinFlipAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Coin == null)
        {
            await FollowupAsync("🪙 Esta mesa não tem uma moeda ativa. Use `/cassino caraoucoroa` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.Coin;
        game.Flip();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento de cara ou coroa",
            RelicGameType.Coin, session.Bet);

        var resultSection = DescribeResult(game, returnAmount)
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CoinTableBuilder.BuildCoinFlipTable(game, game.Side, Context.User, payout.Balance, resultSection);
            m.Components = CoinTableBuilder.BuildCoinResultComponents(Context.User.Id, session.Bet, game.Side);
        });
    }

    [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task CoinReplayAsync(ulong ownerId, ulong bet, int side)
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
            await FollowupAsync("🪙 Você já tem uma moeda no ar! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova rodada em cara ou coroa");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new CoinFlipGame((CoinSide)side);
        _sessions.Add(Context.User.Id, CasinoSession.ForCoin(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CoinTableBuilder.BuildCoinFlipTable(game, game.Side, Context.User, balance);
            m.Components = CoinTableBuilder.BuildCoinComponents();
        });
    }

    [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.PaytableAction, true)]
    public async Task CoinPaytableAsync()
        => await RespondAsync(embed: CoinTableBuilder.BuildCoinPaytable(), ephemeral: true);

    [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.LeaveAction + ":*", true)]
    public async Task CoinLeaveAsync(ulong ownerId)
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
        if (expired.Coin is { } coin && !coin.HasFlipped)
        {
            coin.Flip();
            await _play.PayOutAsync(
                Context.User.Id, coin.CalculateReturn(expired.Bet), Context.User.Username,
                "Cara ou coroa expirada", RelicGameType.Coin, expired.Bet);
        }
    }

    private static string DescribeResult(CoinFlipGame game, ulong returnAmount)
        => returnAmount > 0
            ? $"🎉 **{CoinTableBuilder.DescribeSide(game.Side)}** — você venceu! Recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n"
            : $"😢 Caiu **{CoinTableBuilder.DescribeSide(game.Result!.Value)}** — você perdeu. Boa sorte na próxima!\n";

    private static CoinSide ToSide(CoinSideChoice choice) => choice switch
    {
        CoinSideChoice.Cara => CoinSide.Cara,
        CoinSideChoice.Coroa => CoinSide.Coroa,
        _ => CoinSide.Cara
    };
}

public enum CoinSideChoice
{
    [ChoiceDisplay("Cara")]
    Cara,
    [ChoiceDisplay("Coroa")]
    Coroa
}