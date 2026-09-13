using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class HighLowSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public HighLowSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("altobaixo", "Acerte se a próxima carta é maior ou menor")]
        public async Task HighLowAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.HighLow, Context.User.Id, amount, null,
                "🃏 Você já tem uma rodada em andamento! Use os botões da rodada aberta.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no maior/menor"))
                return;

            _bets.Set(Context.User.Id, amount);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: HighLowTableBuilder.BuildHighLowTable(opened.State.HighLow!, Context.User, balance),
                components: HighLowTableBuilder.BuildHighLowComponents(opened.State.HighLow!, Context.User.Id));
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

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.HighLow, Context.User.Id, "cashout", null,
                "🃏 Esta rodada não está mais ativa. Use `/cassino altobaixo` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Pagamento do maior/menor", RelicGameType.HighLow, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.HighLow!;
            var resultSection = $"🪂 Você saiu em **{HighLowTableBuilder.FormatMultiplier(state.Multiplier)}** e resgatou "
                + $"**{EconomyFormat.Full(done.Outcome?.ReturnAmount ?? 0)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = HighLowTableBuilder.BuildHighLowTable(state, Context.User, payout.Balance, resultSection);
                m.Components = HighLowTableBuilder.BuildHighLowResultComponents(Context.User.Id, bet);
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

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.HighLow, Context.User.Id, bet, null,
                "🃏 Você já tem uma rodada em andamento! Use os botões da rodada aberta.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova rodada de maior/menor", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = HighLowTableBuilder.BuildHighLowTable(opened.State.HighLow!, Context.User, balance);
                m.Components = HighLowTableBuilder.BuildHighLowComponents(opened.State.HighLow!, Context.User.Id);
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

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.HighLow, Context.User.Id);
            _bets.Remove(Context.User.Id);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
            });
        }

        private async Task GuessAsync(bool higher)
        {
            await DeferAsync();

            var userId = Context.User.Id;
            var previousCard = _bets.GetState(userId)?.HighLow?.CurrentCard;

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.HighLow, userId, "guess",
                new ActionPayload { Higher = higher },
                "🃏 Esta rodada não está mais ativa. Use `/cassino altobaixo` para começar outra.");
            if (done == null)
                return;

            if (done.Outcome is { } outcome)
            {
                var bet = _bets.Get(userId);
                var payout = await _play.PayOutAsync(
                    userId, outcome.ReturnAmount, Context.User.Username, "Errou no maior/menor",
                    RelicGameType.HighLow, bet);
                _bets.Remove(userId);

                var resultState = done.State.HighLow!;
                var isImpossiblePick = previousCard != null && resultState.CurrentCard.Rank == previousCard.Rank;
                var resultSection = (isImpossiblePick
                        ? $"❌ Jogada impossível com **`{SymbolText(previousCard!)}`** — você perdeu na hora! Perdeu "
                        : $"❌ A carta era **`{SymbolText(previousCard)}`** e a próxima foi **`{SymbolText(resultState.CurrentCard)}`** — você errou! Perdeu ")
                    + $"{EconomyFormat.Full(bet)} moedas.\n"
                    + CasinoTableBuilder.DescribeAppliedRelic(payout);

                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = HighLowTableBuilder.BuildHighLowTable(resultState, Context.User, payout.Balance, resultSection);
                    m.Components = HighLowTableBuilder.BuildHighLowResultComponents(userId, bet);
                });
                return;
            }

            _bets.SetState(userId, done.State);
            var balance = await _play.GetBalanceAsync(userId, Context.User.Username);
            var state = done.State.HighLow!;

            var isTie = previousCard != null && state.CurrentCard.Rank == previousCard.Rank;
            var section = isTie
                ? $"🤝 Empate! **`{SymbolText(previousCard!)}`** → **`{SymbolText(state.CurrentCard)}`** — carta trocada."
                : $"✅ Acertou! **`{SymbolText(previousCard)}`** → **`{SymbolText(state.CurrentCard)}`** — carta atual!";

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = HighLowTableBuilder.BuildHighLowTable(state, Context.User, balance, section);
                m.Components = HighLowTableBuilder.BuildHighLowComponents(state, Context.User.Id);
            });
        }

        private static string SymbolText(CardDto? card)
            => card is null ? "?" : HighLowTableBuilder.DescribeCard(card);

        private async Task SettleExpiredAsync(Outcome expired)
        {
            await _play.PayOutAsync(
                Context.User.Id, expired.ReturnAmount, Context.User.Username,
                "Maior/menor expirado", RelicGameType.HighLow, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.HighLow, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }
    }
}