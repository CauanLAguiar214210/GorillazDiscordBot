using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class VideoPokerSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public VideoPokerSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("poker", "Joga poker de máquina (Jacks or Better)")]
        public async Task VideoPokerAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.VideoPoker, Context.User.Id, amount, null,
                "🃏 Você já tem uma mão aberta! Use os botões da mesa existente.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no poker de máquina"))
                return;

            _bets.Set(Context.User.Id, amount);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: VideoPokerTableBuilder.BuildVideoPokerTable(opened.State.VideoPoker!, amount, Context.User, balance),
                components: VideoPokerTableBuilder.BuildHoldComponents(opened.State.VideoPoker!, Context.User.Id));
        }

        [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.HoldAction + ":*", true)]
        public async Task VideoPokerHoldAsync(int position)
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.VideoPoker, Context.User.Id, "hold",
                new ActionPayload { HoldPosition = position },
                "🃏 Esta mesa não tem uma mão ativa. Use `/cassino poker` para começar outra.");
            if (done == null)
                return;

            _bets.SetState(Context.User.Id, done.State);

            var bet = _bets.Get(Context.User.Id);
            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            var state = done.State.VideoPoker!;

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(state, bet, Context.User, balance);
                m.Components = VideoPokerTableBuilder.BuildHoldComponents(state, Context.User.Id);
            });
        }

        [ComponentInteraction(VideoPokerTableBuilder.CustomIdPrefix + VideoPokerTableBuilder.DrawAction, true)]
        public async Task VideoPokerDrawAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.VideoPoker, Context.User.Id, "draw", null,
                "🃏 Esta mesa não tem uma mão ativa. Use `/cassino poker` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Pagamento do poker", RelicGameType.VideoPoker, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.VideoPoker!;
            var resultSection = DescribeResult(state, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(state, bet, Context.User, payout.Balance, resultSection);
                m.Components = VideoPokerTableBuilder.BuildVideoPokerResultComponents(Context.User.Id, bet);
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

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.VideoPoker, Context.User.Id, bet, null,
                "🃏 Você já tem uma mão aberta! Use os botões da mesa existente.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova mão no poker de máquina", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = VideoPokerTableBuilder.BuildVideoPokerTable(opened.State.VideoPoker!, bet, Context.User, balance);
                m.Components = VideoPokerTableBuilder.BuildHoldComponents(opened.State.VideoPoker!, Context.User.Id);
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

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.VideoPoker, Context.User.Id);
            _bets.Remove(Context.User.Id);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
            });
        }

        private async Task SettleExpiredAsync(Outcome expired)
        {
            await _play.PayOutAsync(
                Context.User.Id, expired.ReturnAmount, Context.User.Username,
                "Poker de máquina expirado", RelicGameType.VideoPoker, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.VideoPoker, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.VideoPokerState state, ulong returnAmount)
            => returnAmount == 0
                ? $"😢 Nenhuma combinação — perdeu. Boa sorte na próxima!\n"
                : $"🎉 **{VideoPokerTableBuilder.DescribeOutcome(state.Outcome ?? VideoPokerHandOutcome.None)}** — você venceu! "
                    + $"Recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n";
    }
}