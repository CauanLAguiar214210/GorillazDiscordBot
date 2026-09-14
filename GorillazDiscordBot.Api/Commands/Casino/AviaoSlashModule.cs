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
    public class AviaoSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public AviaoSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("aviaozinho", "Aposte no aviaozinho botão por botão")]
        public async Task AviaoAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Aviao, Context.User.Id, amount, null,
                "✈️ Você já tem um voo em andamento! Use os botões do voo aberto.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no aviaozinho"))
                return;

            _bets.Set(Context.User.Id, amount);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: AviaoTableBuilder.BuildAviaoTable(opened.State.Aviao!, Context.User, balance),
                components: AviaoTableBuilder.BuildFlightComponents(opened.State.Aviao!));
        }

        [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.FlyAction, true)]
        public async Task AviaoFlyAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Aviao, Context.User.Id, "fly", null,
                "✈️ Este voo não está mais ativo. Use `/cassino aviaozinho` para começar outro.");
            if (done == null)
                return;

            if (done.Outcome is { } outcome)
            {
                var bet = _bets.Get(Context.User.Id);
                var payout = await _play.PayOutAsync(
                    Context.User.Id, outcome.ReturnAmount, Context.User.Username, "Aviaozinho explodiu",
                    RelicGameType.Aviao, bet);
                _bets.Remove(Context.User.Id);

                var resultState = done.State.Aviao!;
                var resultSection = $"💥 O aviãozinho explodiu em **{AviaoTableBuilder.FormatMultiplier(resultState.CrashMultiplier!.Value)}**! "
                    + $"Perdeu **{EconomyFormat.Full(bet)}** moedas.\n"
                    + CasinoTableBuilder.DescribeAppliedRelic(payout);

                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = AviaoTableBuilder.BuildAviaoTable(resultState, Context.User, payout.Balance, resultSection);
                    m.Components = AviaoTableBuilder.BuildAviaoResultComponents(Context.User.Id, bet);
                });
                return;
            }

            _bets.SetState(Context.User.Id, done.State);
            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            var state = done.State.Aviao!;

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = AviaoTableBuilder.BuildAviaoTable(state, Context.User, balance);
                m.Components = AviaoTableBuilder.BuildFlightComponents(state);
            });
        }

        [ComponentInteraction(AviaoTableBuilder.CustomIdPrefix + AviaoTableBuilder.CashOutAction, true)]
        public async Task AviaoCashOutAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Aviao, Context.User.Id, "cashout", null,
                "✈️ Este voo não está mais ativo. Use `/cassino aviaozinho` para começar outro.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username, "Pagamento do aviaozinho",
                RelicGameType.Aviao, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Aviao!;
            var resultSection = $"🪂 Você pulou em **{AviaoTableBuilder.FormatMultiplier(state.CurrentMultiplier)}** e resgatou "
                + $"**{EconomyFormat.Full(done.Outcome?.ReturnAmount ?? 0)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = AviaoTableBuilder.BuildAviaoTable(state, Context.User, payout.Balance, resultSection);
                m.Components = AviaoTableBuilder.BuildAviaoResultComponents(Context.User.Id, bet);
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

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Aviao, Context.User.Id, bet, null,
                "✈️ Você já tem um voo em andamento! Use os botões do voo aberto.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Novo voo no aviaozinho", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = AviaoTableBuilder.BuildAviaoTable(opened.State.Aviao!, Context.User, balance);
                m.Components = AviaoTableBuilder.BuildFlightComponents(opened.State.Aviao!);
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

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Aviao, Context.User.Id);
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
                "Aviaozinho expirado", RelicGameType.Aviao, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Aviao, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }
    }
}