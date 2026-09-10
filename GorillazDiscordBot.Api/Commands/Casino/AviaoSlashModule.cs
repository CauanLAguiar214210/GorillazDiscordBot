using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public class AviaoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public AviaoSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("aviaozinho", "Aposte no aviaozinho botão por botão")]
    public async Task AviaoAsync(
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
            await RespondAsync("✈️ Você já tem um voo em andamento! Use os botões do voo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no aviaozinho");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new AviaoGame(amount);
        _sessions.Add(Context.User.Id, CasinoSession.ForAviao(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: AviaoTableBuilder.BuildAviaoTable(game, Context.User, balance),
            components: AviaoTableBuilder.BuildFlightComponents(game));
    }

    [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.FlyAction, true)]
    public async Task AviaoFlyAsync()
    {
        await DeferAsync();

        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Aviao == null)
        {
            await FollowupAsync("✈️ Este voo não está mais ativo. Use `/aviaozinho` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Aviao;
        game.Fly();

        if (game.HasCrashed)
        {
            _sessions.Remove(Context.User.Id);

            var payout = await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Aviaozinho explodiu",
                RelicGameType.Aviao, session.Bet);

            var resultSection = $"💥 O aviãozinho explodiu em **{AviaoTableBuilder.FormatMultiplier(game.CrashMultiplier)}**! "
                + $"Perdeu **{EconomyFormat.Full(session.Bet)}** moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = AviaoTableBuilder.BuildAviaoTable(game, Context.User, payout.Balance, resultSection);
                m.Components = AviaoTableBuilder.BuildAviaoResultComponents(Context.User.Id, session.Bet);
            });
            return;
        }

        _sessions.Touch(Context.User.Id);
        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = AviaoTableBuilder.BuildAviaoTable(game, Context.User, balance);
            m.Components = AviaoTableBuilder.BuildFlightComponents(game);
        });
    }

    [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.CashOutAction, true)]
    public async Task AviaoCashOutAsync()
    {
        await DeferAsync();

        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Aviao == null)
        {
            await FollowupAsync("✈️ Este voo não está mais ativo. Use `/aviaozinho` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Aviao;
        var returnAmount = game.CashOut();
        _sessions.Remove(Context.User.Id);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento do aviaozinho",
            RelicGameType.Aviao, session.Bet);

        var resultSection = $"🪂 Você pulou em **{AviaoTableBuilder.FormatMultiplier(game.CurrentMultiplier)}** e resgatou "
            + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = AviaoTableBuilder.BuildAviaoTable(game, Context.User, payout.Balance, resultSection);
            m.Components = AviaoTableBuilder.BuildAviaoResultComponents(Context.User.Id, session.Bet);
        });
    }

    [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.ReplayAction + ":*:*", true)]
    public async Task AviaoReplayAsync(ulong ownerId, ulong bet)
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
            await FollowupAsync("✈️ Você já tem um voo em andamento! Use os botões do voo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Novo voo no aviaozinho");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new AviaoGame(bet);
        _sessions.Add(Context.User.Id, CasinoSession.ForAviao(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = AviaoTableBuilder.BuildAviaoTable(game, Context.User, balance);
            m.Components = AviaoTableBuilder.BuildFlightComponents(game);
        });
    }

    [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.PaytableAction, true)]
    public async Task AviaoPaytableAsync()
        => await RespondAsync(embed: AviaoTableBuilder.BuildAviaoPaytable(), ephemeral: true);

    [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.LeaveAction + ":*", true)]
    public async Task AviaoLeaveAsync(ulong ownerId)
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
        if (expired.Aviao is { } aviao && !aviao.IsFinished)
        {
            aviao.Fly();
            await _play.PayOutAsync(
                Context.User.Id, 0, Context.User.Username, "Aviaozinho expirado",
                RelicGameType.Aviao, expired.Bet);
        }
    }
}