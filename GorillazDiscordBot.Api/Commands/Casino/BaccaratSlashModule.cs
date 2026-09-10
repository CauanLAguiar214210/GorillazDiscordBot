using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

[Group("cassino", "Jogos de cassino")]
public class BaccaratSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public BaccaratSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("baccarat", "Aposte no jogador, no banco ou no empate")]
    public async Task BaccaratAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("aposta", "Em quem você quer apostar")] BaccaratBetChoice aposta)
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
            await RespondAsync("🎴 Você já tem uma partida em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no baccarat");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new BaccaratGame(amount, ToBetType(aposta));
        _sessions.Add(Context.User.Id, CasinoSession.ForBaccarat(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: BaccaratTableBuilder.BuildBaccaratTable(game, Context.User, balance),
            components: BaccaratTableBuilder.BuildBaccaratComponents(Context.User.Id));
    }

    [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.RevealAction, true)]
    public async Task BaccaratRevealAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Baccarat == null)
        {
            await FollowupAsync("🎴 Esta mesa não está mais ativa. Use `/cassino baccarat` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.Baccarat;
        game.Reveal();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado do baccarat",
            RelicGameType.Baccarat, session.Bet);

        var resultSection = DescribeResult(game, returnAmount)
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BaccaratTableBuilder.BuildBaccaratTable(game, Context.User, payout.Balance, resultSection);
            m.Components = BaccaratTableBuilder.BuildBaccaratResultComponents(
                Context.User.Id, session.Bet, (int)game.BetType);
        });
    }

    [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task BaccaratReplayAsync(ulong ownerId, ulong bet, int betType)
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
            await FollowupAsync("🎴 Você já tem uma partida em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova mão de baccarat");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new BaccaratGame(bet, (BaccaratBetType)betType);
        _sessions.Add(Context.User.Id, CasinoSession.ForBaccarat(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BaccaratTableBuilder.BuildBaccaratTable(game, Context.User, balance);
            m.Components = BaccaratTableBuilder.BuildBaccaratComponents(Context.User.Id);
        });
    }

    [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.PaytableAction, true)]
    public async Task BaccaratPaytableAsync()
        => await RespondAsync(embed: BaccaratTableBuilder.BuildBaccaratPaytable(), ephemeral: true);

    [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.LeaveAction + ":*", true)]
    public async Task BaccaratLeaveAsync(ulong ownerId)
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
        if (expired.Baccarat is { } baccarat && !baccarat.HasRevealed)
        {
            baccarat.Reveal();
            await _play.PayOutAsync(
                Context.User.Id, baccarat.CalculateReturn(expired.Bet), Context.User.Username, "Baccarat expirado",
                RelicGameType.Baccarat, expired.Bet);
        }
    }

    private static string DescribeResult(BaccaratGame game, ulong returnAmount)
    {
        if (game.Outcome == BaccaratOutcome.Tie)
            return $"🤝 **Empate!** Jogador e banco têm **{game.Player.Value}**. "
                + $"Você recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n";

        var winner = game.Outcome == BaccaratOutcome.PlayerWin ? "Jogador" : "Banco";

        return game.IsWin
            ? $"🎉 **{winner} venceu!** Você recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n"
            : $"😢 **{winner} venceu.** Você apostou no outro lado e perdeu "
                + $"**{EconomyFormat.Full(game.Bet)}** moedas.\n";
    }

    private static BaccaratBetType ToBetType(BaccaratBetChoice choice) => (BaccaratBetType)choice;
}

public enum BaccaratBetChoice
{
    [ChoiceDisplay("Jogador ✋ (2x)")]
    Jogador,
    [ChoiceDisplay("Banco 🏦 (1.95x)")]
    Banco,
    [ChoiceDisplay("Empate 🤝 (9x)")]
    Empate
}