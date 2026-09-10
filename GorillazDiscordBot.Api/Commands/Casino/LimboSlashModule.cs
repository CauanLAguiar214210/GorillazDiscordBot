using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class LimboSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public LimboSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("limbo", "El número sorteado que pasa del objetivo multiplica la apuesta")]
    public async Task LimboAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M, 1B")] string valor,
        [Summary("alvo", "Multiplicador alvo que você quer alcançar")] LimboTargetChoice alvo = LimboTargetChoice.DuasVezes)
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
            await RespondAsync("🔮 Você já tem um limbo em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta no limbo");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new LimboGame(toTarget(alvo));
        _sessions.Add(Context.User.Id, CasinoSession.ForLimbo(amount, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: LimboTableBuilder.BuildLimboTable(game, Context.User, balance),
            components: LimboTableBuilder.BuildLimboComponents());
    }

    [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.RevealAction, true)]
    public async Task LimboRevealAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Limbo == null)
        {
            await FollowupAsync("🔮 Este limbo não está mais ativo. Use `/cassino limbo` para começar outro.", ephemeral: true);
            return;
        }

        var game = session.Limbo;
        game.Roll();
        _sessions.Remove(Context.User.Id);

        var returnAmount = game.CalculateReturn(session.Bet);

        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username,
            game.IsWin ? "Ganhou no limbo" : "Perdeu no limbo",
            RelicGameType.Limbo, session.Bet);

        var resultSection = game.IsWin
            ? $"✅ O sorteado **{LimboTableBuilder.FormatMultiplier(game.Result!.Value)}** passou de "
                + $"**{LimboTableBuilder.FormatMultiplier(game.Target)}** e você ganhou "
                + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout)
            : $"❌ O sorteado **{LimboTableBuilder.FormatMultiplier(game.Result!.Value)}** ficou abaixo de "
                + $"**{LimboTableBuilder.FormatMultiplier(game.Target)}**. Perdeu "
                + $"**{EconomyFormat.Full(session.Bet)}** moedas.\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = LimboTableBuilder.BuildLimboTable(game, Context.User, payout.Balance, resultSection);
            m.Components = LimboTableBuilder.BuildLimboResultComponents(
                Context.User.Id, session.Bet, (int)(game.Target * 100));
        });
    }

    [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.ReplayAction + ":*:*:*", true)]
    public async Task LimboReplayAsync(ulong ownerId, ulong bet, int targetCents)
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
            await FollowupAsync("🔮 Você já tem um limbo em andamento! Use os botões do jogo aberto.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, bet, Context.User.Username, "Novo limbo");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new LimboGame(targetCents / 100d);
        _sessions.Add(Context.User.Id, CasinoSession.ForLimbo(bet, game));

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = LimboTableBuilder.BuildLimboTable(game, Context.User, balance);
            m.Components = LimboTableBuilder.BuildLimboComponents();
        });
    }

    [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.PaytableAction, true)]
    public async Task LimboPaytableAsync()
        => await RespondAsync(embed: LimboTableBuilder.BuildLimboPaytable(), ephemeral: true);

    [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.LeaveAction + ":*", true)]
    public async Task LimboLeaveAsync(ulong ownerId)
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
        if (expired.Limbo is { } limbo && !limbo.HasRolled)
        {
            limbo.Roll();
            await _play.PayOutAsync(
                Context.User.Id, limbo.CalculateReturn(expired.Bet), Context.User.Username, "Limbo expirado",
                RelicGameType.Limbo, expired.Bet);
        }
    }

    private static double toTarget(LimboTargetChoice choice) => ((int)choice) / 100d;
    }
}

public enum LimboTargetChoice
{
    [ChoiceDisplay("2x")]
    DuasVezes = 200,
    [ChoiceDisplay("3x")]
    TresVezes = 300,
    [ChoiceDisplay("5x")]
    CincoVezes = 500,
    [ChoiceDisplay("10x")]
    DezVezes = 1000
}