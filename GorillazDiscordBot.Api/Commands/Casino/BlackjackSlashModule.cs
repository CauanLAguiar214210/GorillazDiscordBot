using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Casino;

public class BlackjackSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEconomyRepository _economy;
    private readonly GameSessionManager _sessions;

    public BlackjackSlashModule(IEconomyRepository economy, GameSessionManager sessions)
    {
        _economy = economy;
        _sessions = sessions;
    }

    [SlashCommand("blackjack", "Inicia uma mão de Blackjack com botões")]
    public async Task BlackjackAsync(
        [Summary("valor", "Quantidade de moedas para apostar")] int valor)
    {
        if (valor <= 0)
        {
            await RespondAsync("⚠️ O valor da aposta deve ser positivo.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id;

        var expired = _sessions.TakeExpired(userId);
        if (expired != null)
            expired.Stand();

        if (_sessions.GetActive(userId) != null)
        {
            await RespondAsync("🃏 Você já tem uma mão em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            userId, valor, EconomyTransactionType.Bet, "Aposta no blackjack");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new BlackjackGame(valor);

        if (game.Phase == BlackjackPhase.Finished)
        {
            var settledEmbed = await SettleAndBuildAsync(game);
            await RespondAsync(
                embed: settledEmbed,
                components: BlackjackTableBuilder.BuildResultComponents(Context.User.Id, game.Bet));

            if (expired != null)
                await ReportExpiredAsync(expired);

            return;
        }

        _sessions.Add(userId, game);

        await RespondAsync(
            embed: BlackjackTableBuilder.BuildTable(game, Context.User),
            components: BlackjackTableBuilder.BuildActionComponents(game));

        if (expired != null)
            await ReportExpiredAsync(expired);
    }

    [ComponentInteraction("bj:*", true)]
    public async Task BlackjackActionAsync(string action)
    {
        await DeferAsync();

        var userId = Context.User.Id;

        var expired = _sessions.TakeExpired(userId);
        if (expired != null)
        {
            expired.Stand();
            var expiredEmbed = await SettleAndBuildAsync(expired, "⏳ Sua mão anterior expirou e o dealer jogou por você.\n\n");
            await ReplaceWithResultAsync(expiredEmbed, Context.User.Id, expired.Bet);
            return;
        }

        var game = _sessions.GetActive(userId);

        if (game == null)
        {
            await FollowupAsync("🃏 Esta mesa não tem mais uma mão ativa ou não é sua. Use `/blackjack` para começar outra.", ephemeral: true);
            return;
        }

        switch (action)
        {
            case BlackjackTableBuilder.PaytableAction:
                await FollowupAsync(embed: BlackjackTableBuilder.BuildBlackjackPaytable(), ephemeral: true);
                return;

            case BlackjackTableBuilder.HitAction:
                game.Hit();
                _sessions.Touch(userId);
                break;

            case BlackjackTableBuilder.StandAction:
                game.Stand();
                break;

            case BlackjackTableBuilder.DoubleAction:
                if (!BlackjackTableBuilder.CanDouble(game))
                {
                    await FollowupAsync("⚠️ Dobrar é permitido apenas com as duas primeiras cartas.", ephemeral: true);
                    return;
                }

                var (deducted, _) = await _economy.TryDeductMoneyAsync(
                    userId, game.Bet, EconomyTransactionType.Bet, "Double no blackjack");

                if (!deducted)
                {
                    await FollowupAsync("❌ Você não tem moedas suficientes na carteira para dobrar.", ephemeral: true);
                    return;
                }

                game.DoubleDown();
                break;

            default:
                await FollowupAsync("Ação desconhecida.", ephemeral: true);
                return;
        }

        if (game.Phase == BlackjackPhase.Finished)
        {
            var embed = await SettleAndBuildAsync(game);
            await ReplaceWithResultAsync(embed, Context.User.Id, game.Bet);
        }
        else
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BlackjackTableBuilder.BuildTable(game, Context.User);
                m.Components = BlackjackTableBuilder.BuildActionComponents(game);
            });
        }
    }

    [ComponentInteraction(BlackjackTableBuilder.ResultCustomIdPrefix + BlackjackTableBuilder.ReplayAction + ":*:*", true)]
    public async Task BlackjackReplayAsync(ulong ownerId, int bet)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        var userId = Context.User.Id;

        var expired = _sessions.TakeExpired(userId);
        if (expired != null)
        {
            expired.Stand();
            var expiredEmbed = await SettleAndBuildAsync(expired, "⏳ Sua mão anterior expirou e o dealer jogou por você.\n\n");
            await ReplaceWithResultAsync(expiredEmbed, ownerId, expired.Bet);
            return;
        }

        if (_sessions.GetActive(userId) != null)
        {
            await FollowupAsync("🃏 Você já tem uma mão em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            userId, bet, EconomyTransactionType.Bet, "Nova mão no blackjack");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new BlackjackGame(bet);

        if (game.Phase == BlackjackPhase.Finished)
        {
            var settledEmbed = await SettleAndBuildAsync(game);
            await ReplaceWithResultAsync(settledEmbed, ownerId, game.Bet);
            return;
        }

        _sessions.Add(userId, game);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BlackjackTableBuilder.BuildTable(game, Context.User);
            m.Components = BlackjackTableBuilder.BuildActionComponents(game);
        });
    }

    [ComponentInteraction(BlackjackTableBuilder.ResultCustomIdPrefix + BlackjackTableBuilder.LeaveAction + ":*", true)]
    public async Task BlackjackLeaveAsync(ulong ownerId)
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

    [ComponentInteraction(BlackjackTableBuilder.ResultCustomIdPrefix + BlackjackTableBuilder.PaytableAction, true)]
    public async Task BlackjackResultPaytableAsync()
        => await RespondAsync(embed: BlackjackTableBuilder.BuildBlackjackPaytable(), ephemeral: true);

    private async Task<Embed> SettleAndBuildAsync(BlackjackGame game, string prefix = "")
    {
        _sessions.Remove(Context.User.Id);

        var totalReturn = game.CalculateTotalReturn();

        if (totalReturn > 0)
            await _economy.AddMoneyAsync(Context.User.Id, totalReturn, EconomyTransactionType.Bet, "Pagamento do blackjack");

        var user = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        var resultSection = prefix
            + BlackjackTableBuilder.DescribeResult(game, totalReturn)
            + $"\n💰 Saldo atual: **{user.Money}** moedas";

        return BlackjackTableBuilder.BuildTable(game, Context.User, resultSection);
    }

    private Task ReplaceWithResultAsync(Embed embed, ulong ownerId, int bet)
        => Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = BlackjackTableBuilder.BuildResultComponents(ownerId, bet);
        });

    private async Task ReportExpiredAsync(BlackjackGame expired)
    {
        var embed = await SettleAndBuildAsync(expired, "⏳ Sua mão anterior expirou e o dealer jogou por você.\n\n");
        await FollowupAsync(embed: embed, ephemeral: true);
    }
}