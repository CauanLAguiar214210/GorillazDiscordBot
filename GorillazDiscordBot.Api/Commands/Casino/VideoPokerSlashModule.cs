using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public class VideoPokerSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public VideoPokerSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("poker", "Joga poker de máquina (Jacks or Better)")]
    public async Task VideoPokerAsync(
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
            await RespondAsync("🃏 Você já tem uma mão aberta! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no poker de máquina");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new VideoPokerGame();
        _sessions.Add(Context.User.Id, CasinoSession.ForVideoPoker(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: VideoPokerTableBuilder.BuildVideoPokerTable(game, amount, Context.User, balance),
            components: VideoPokerTableBuilder.BuildHoldComponents(game, Context.User.Id));
    }

    [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.HoldAction + ":*", true)]
    public async Task VideoPokerHoldAsync(int position)
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.VideoPoker == null)
        {
            await FollowupAsync("🃏 Esta mesa não tem uma mão ativa. Use `/poker` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.VideoPoker;
        game.Hold(position);

        _sessions.Touch(Context.User.Id);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(game, session.Bet, Context.User, balance);
            m.Components = VideoPokerTableBuilder.BuildHoldComponents(game, Context.User.Id);
        });
    }

    [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.DrawAction, true)]
    public async Task VideoPokerDrawAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.VideoPoker == null)
        {
            await FollowupAsync("🃏 Esta mesa não tem uma mão ativa. Use `/poker` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.VideoPoker;
        game.Draw();
        _sessions.Remove(Context.User.Id);

        var outcome = game.Evaluate();
        var returnAmount = VideoPokerGame.CalculateReturn(outcome, session.Bet);
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento do poker",
            RelicGameType.VideoPoker, session.Bet);

        var resultSection = DescribeResult(outcome, returnAmount)
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(game, session.Bet, Context.User, payout.Balance, resultSection);
            m.Components = VideoPokerTableBuilder.BuildVideoPokerResultComponents(Context.User.Id, session.Bet);
        });
    }

    [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.ReplayAction + ":*:*", true)]
    public async Task VideoPokerReplayAsync(ulong ownerId, ulong bet)
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
            await FollowupAsync("🃏 Você já tem uma mão aberta! Use os botões da mesa existente.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Nova mão no poker de máquina");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new VideoPokerGame();
        _sessions.Add(Context.User.Id, CasinoSession.ForVideoPoker(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(game, bet, Context.User, balance);
            m.Components = VideoPokerTableBuilder.BuildHoldComponents(game, Context.User.Id);
        });
    }

    [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.PaytableAction, true)]
    public async Task VideoPokerPaytableAsync()
        => await RespondAsync(embed: VideoPokerTableBuilder.BuildVideoPokerPaytable(), ephemeral: true);

    [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.LeaveAction + ":*", true)]
    public async Task VideoPokerLeaveAsync(ulong ownerId)
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
        if (expired.VideoPoker is { } poker && !poker.HasDrawn)
        {
            poker.Draw();
            var outcome = poker.Evaluate();
            await _play.PayOutAsync(
                Context.User.Id, VideoPokerGame.CalculateReturn(outcome, expired.Bet),
                Context.User.Username, "Poker de máquina expirado", RelicGameType.VideoPoker, expired.Bet);
        }
    }

    private static string DescribeResult(VideoPokerHandOutcome outcome, ulong returnAmount)
    {
        if (returnAmount == 0)
            return $"😢 Nenhuma combinação — perdeu. Boa sorte na próxima!\n";
        return $"🎉 **{VideoPokerTableBuilder.DescribeOutcome(outcome)}** — você venceu! Recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n";
    }
}