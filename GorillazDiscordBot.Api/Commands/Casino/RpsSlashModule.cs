using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class RpsSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public RpsSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("jokenpo", "Pedra, papel e tesoura valendo moedas")]
    public async Task RpsAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("jogada", "Sua jogada")] RpsMoveChoice jogada)
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
            await RespondAsync("🤚 Você já tem um jokenpô em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no jokenpô");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new RpsGame(toMove(jogada));
        _sessions.Add(Context.User.Id, CasinoSession.ForRps(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: RpsTableBuilder.BuildRpsTable(game, amount, Context.User, balance),
            components: RpsTableBuilder.BuildRpsComponents());
    }

    [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.PlayAction, true)]
    public async Task RpsPlayAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Rps == null)
        {
            await FollowupAsync("🤚 Este jokenpô não está mais ativo. Use `/cassino jokenpo` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Rps;
        game.Play();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Resultado do jokenpô",
            RelicGameType.Rps, session.Bet);

        var resultSection = game.IsWin
            ? $"🏆 **Vitória!** Oponente jogou **{RpsTableBuilder.DescribeMove(game.OpponentMove!.Value)}** "
                + $"e você faturou **{EconomyFormat.Full(returnAmount)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout)
            : game.IsDraw
                ? $"🤝 **Empate!** Oponente também jogou **{RpsTableBuilder.DescribeMove(game.OpponentMove!.Value)}**. "
                    + $"A aposta de **{EconomyFormat.Full(session.Bet)}** foi devolvida.\n"
                    + CasinoTableBuilder.DescribeAppliedRelic(payout)
                : $"❌ **Derrota.** Oponente jogou **{RpsTableBuilder.DescribeMove(game.OpponentMove!.Value)}**. "
                    + $"Perdeu **{EconomyFormat.Full(session.Bet)}** moedas.\n"
                    + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = RpsTableBuilder.BuildRpsTable(
                game, session.Bet, Context.User, payout.Balance, resultSection);
            m.Components = RpsTableBuilder.BuildRpsResultComponents(
                Context.User.Id, session.Bet, (int)game.PlayerMove);
        });
    }

    [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task RpsReplayAsync(ulong ownerId, ulong bet, int move)
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
            await FollowupAsync("🤚 Você já tem um jokenpô em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova rodada de jokenpô");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new RpsGame((RpsMove)move);
        _sessions.Add(Context.User.Id, CasinoSession.ForRps(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = RpsTableBuilder.BuildRpsTable(game, bet, Context.User, balance);
            m.Components = RpsTableBuilder.BuildRpsComponents();
        });
    }

    [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.PaytableAction, true)]
    public async Task RpsPaytableAsync()
        => await RespondAsync(embed: RpsTableBuilder.BuildRpsPaytable(), ephemeral: true);

    [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.LeaveAction + ":*", true)]
    public async Task RpsLeaveAsync(ulong ownerId)
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
        if (expired.Rps is { } rps && !rps.HasPlayed)
        {
            rps.Play();
            await _play.PayOutAsync(
                Context.User.Id, rps.CalculateReturn(expired.Bet), Context.User.Username, "Jokenpô expirado",
                RelicGameType.Rps, expired.Bet);
        }
    }

    private static RpsMove toMove(RpsMoveChoice choice) => (RpsMove)choice;
    }
}

public enum RpsMoveChoice
{
    [ChoiceDisplay("Pedra ✊")]
    Pedra,
    [ChoiceDisplay("Papel ✋")]
    Papel,
    [ChoiceDisplay("Tesoura ✌️")]
    Tesoura
}