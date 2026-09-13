using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class BlackjackSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public BlackjackSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("blackjack", "Inicia uma mão de Blackjack com botões")]
        public async Task BlackjackAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var error))
            {
                await RespondAsync(error, ephemeral: true);
                return;
            }

            var userId = Context.User.Id;

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Blackjack, userId, amount, null,
                "🃏 Você já tem uma mão em andamento! Use os botões da mesa aberta.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no blackjack"))
                return;

            _bets.Set(userId, amount);

            if (opened.Settled is { } settled)
            {
                var payout = await _play.PayOutAsync(
                    userId, settled.ReturnAmount, Context.User.Username, "Pagamento do blackjack",
                    RelicGameType.Blackjack, amount);
                _bets.Remove(userId);

                var state = opened.State.Blackjack!;
                var resultSection = BlackjackTableBuilder.DescribeResult(state, settled.ReturnAmount)
                    + CasinoTableBuilder.DescribeAppliedRelic(payout)
                    + $"\n💰 Saldo atual: **{EconomyFormat.Full(payout.Balance)}** moedas";

                await RespondAsync(
                    embed: BlackjackTableBuilder.BuildTable(state, Context.User, resultSection),
                    components: BlackjackTableBuilder.BuildResultComponents(userId, state.Bet));
                return;
            }

            _bets.SetState(userId, opened.State);

            var balance = await _play.GetBalanceAsync(userId, Context.User.Username);
            await RespondAsync(
                embed: BlackjackTableBuilder.BuildTable(opened.State.Blackjack!, Context.User),
                components: BlackjackTableBuilder.BuildActionComponents(opened.State.Blackjack!));
        }

        [ComponentInteraction("bj:*", true)]
        public async Task BlackjackActionAsync(string action)
        {
            await DeferAsync();

            var userId = Context.User.Id;

            switch (action)
            {
                case BlackjackTableBuilder.PaytableAction:
                    await FollowupAsync(embed: BlackjackTableBuilder.BuildBlackjackPaytable(), ephemeral: true);
                    return;

                case BlackjackTableBuilder.HitAction:
                    await BlackjackStepAsync("hit");
                    return;

                case BlackjackTableBuilder.StandAction:
                    await BlackjackStepAsync("stand");
                    return;

                case BlackjackTableBuilder.DoubleAction:
                    await BlackjackDoubleAsync();
                    return;

                default:
                    await FollowupAsync("Ação desconhecida.", ephemeral: true);
                    return;
            }
        }

        [ComponentInteraction(BlackjackTableBuilder.ResultCustomIdPrefix + BlackjackTableBuilder.ReplayAction + ":*:*", true)]
        public async Task BlackjackReplayAsync(ulong ownerId, ulong bet)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var userId = Context.User.Id;

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Blackjack, userId, bet, null,
                "🃏 Você já tem uma mão em andamento! Use os botões da mesa aberta.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova mão no blackjack", followup: true))
                return;

            _bets.Set(userId, bet);

            if (opened.Settled is { } settled)
            {
                var payout = await _play.PayOutAsync(
                    userId, settled.ReturnAmount, Context.User.Username, "Pagamento do blackjack",
                    RelicGameType.Blackjack, bet);
                _bets.Remove(userId);

                var state = opened.State.Blackjack!;
                var resultSection = BlackjackTableBuilder.DescribeResult(state, settled.ReturnAmount)
                    + CasinoTableBuilder.DescribeAppliedRelic(payout)
                    + $"\n💰 Saldo atual: **{EconomyFormat.Full(payout.Balance)}** moedas";

                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = BlackjackTableBuilder.BuildTable(state, Context.User, resultSection);
                    m.Components = BlackjackTableBuilder.BuildResultComponents(ownerId, state.Bet);
                });
                return;
            }

            _bets.SetState(userId, opened.State);

            var balance = await _play.GetBalanceAsync(userId, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BlackjackTableBuilder.BuildTable(opened.State.Blackjack!, Context.User);
                m.Components = BlackjackTableBuilder.BuildActionComponents(opened.State.Blackjack!);
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

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Blackjack, Context.User.Id);
            _bets.Remove(Context.User.Id);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
            });
        }

        [ComponentInteraction(BlackjackTableBuilder.ResultCustomIdPrefix + BlackjackTableBuilder.PaytableAction, true)]
        public async Task BlackjackResultPaytableAsync()
            => await RespondAsync(embed: BlackjackTableBuilder.BuildBlackjackPaytable(), ephemeral: true);

        private async Task BlackjackDoubleAsync()
        {
            var userId = Context.User.Id;

            var current = _bets.GetState(userId)?.Blackjack;
            if (current == null || !current.CanDouble)
            {
                await FollowupAsync("⚠️ Dobrar é permitido apenas com as duas primeiras cartas.", ephemeral: true);
                return;
            }

            var bet = _bets.Get(userId);
            var (deducted, _) = await _play.DeductBetAsync(
                userId, bet, Context.User.Username, "Double no blackjack");

            if (!deducted)
            {
                await FollowupAsync("❌ Você não tem moedas suficientes na carteira para dobrar.", ephemeral: true);
                return;
            }

            await BlackjackStepAsync("double", bet);
        }

        private async Task BlackjackStepAsync(string action, ulong? extraBet = null)
        {
            var userId = Context.User.Id;

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Blackjack, userId, action, null,
                "🃏 Esta mesa não tem mais uma mão ativa ou não é sua. Use `/cassino blackjack` para começar outra.");
            if (done == null)
            {
                if (extraBet is { } bet)
                    await _play.RefundAsync(userId, bet, Context.User.Username, "Reembolso do double no blackjack");
                return;
            }

            _bets.SetState(userId, done.State);

            if (done.Outcome is { } outcome)
            {
                var payout = await _play.PayOutAsync(
                    userId, outcome.ReturnAmount, Context.User.Username, "Pagamento do blackjack",
                    RelicGameType.Blackjack, _bets.Get(userId));
                _bets.Remove(userId);

                var state = done.State.Blackjack!;
                var resultSection = BlackjackTableBuilder.DescribeResult(state, outcome.ReturnAmount)
                    + CasinoTableBuilder.DescribeAppliedRelic(payout)
                    + $"\n💰 Saldo atual: **{EconomyFormat.Full(payout.Balance)}** moedas";

                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = BlackjackTableBuilder.BuildTable(state, Context.User, resultSection);
                    m.Components = BlackjackTableBuilder.BuildResultComponents(userId, state.Bet);
                });
            }
            else
            {
                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = BlackjackTableBuilder.BuildTable(done.State.Blackjack!, Context.User);
                    m.Components = BlackjackTableBuilder.BuildActionComponents(done.State.Blackjack!);
                });
            }
        }

        private async Task SettleExpiredAsync(Outcome expired)
        {
            await _play.PayOutAsync(
                Context.User.Id, expired.ReturnAmount, Context.User.Username,
                "Blackjack expirado", RelicGameType.Blackjack, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Blackjack, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }
    }
}