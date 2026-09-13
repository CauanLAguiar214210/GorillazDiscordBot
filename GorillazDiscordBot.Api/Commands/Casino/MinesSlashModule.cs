using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Bets;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class MinesSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public MinesSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("minas", "Revela células seguras antes de achar uma mina")]
        public async Task MinesAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("minas", "Quantidade de minas na grade")] MinesCountChoice minas = MinesCountChoice.Quatro)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var minesCount = ToMinesCount(minas);

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Mines, Context.User.Id, amount,
                new BetOptions { MinesCount = minesCount },
                "🧨 Você já tem um campo aberto! Use os botões do campo existente.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta nas minas"))
                return;

            _bets.Set(Context.User.Id, amount);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: MinesTableBuilder.BuildMinesTable(opened.State.Mines!, Context.User, balance),
                components: MinesTableBuilder.BuildMinesComponents(opened.State.Mines!, Context.User.Id));
        }

        [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.RevealAction + ":*", true)]
        public async Task MinesRevealAsync(int cell)
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Mines, Context.User.Id, "reveal",
                new ActionPayload { Cell = cell },
                "🧨 Este campo não está mais ativo. Use `/cassino minas` para começar outro.");
            if (done == null)
                return;

            if (done.Outcome is { } outcome)
            {
                var bet = _bets.Get(Context.User.Id);
                var payout = await _play.PayOutAsync(
                    Context.User.Id, outcome.ReturnAmount, Context.User.Username, "Mina encontrada",
                    RelicGameType.Mines, bet);
                _bets.Remove(Context.User.Id);

                var resultState = done.State.Mines!;
                var resultSection = "💥 **Você pisou numa mina!** Perdeu a aposta.\n"
                    + CasinoTableBuilder.DescribeAppliedRelic(payout);

                await Context.Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = MinesTableBuilder.BuildMinesTable(resultState, Context.User, payout.Balance, resultSection);
                    m.Components = MinesTableBuilder.BuildMinesResultComponents(Context.User.Id, bet, resultState.MinesCount);
                });
                return;
            }

            _bets.SetState(Context.User.Id, done.State);
            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            var state = done.State.Mines!;

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = MinesTableBuilder.BuildMinesTable(state, Context.User, balance);
                m.Components = MinesTableBuilder.BuildMinesComponents(state, Context.User.Id);
            });
        }

        [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.CashOutAction, true)]
        public async Task MinesCashOutAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Mines, Context.User.Id, "cashout", null,
                "🧨 Este campo não está mais ativo. Use `/cassino minas` para começar outro.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username, "Pagamento das minas",
                RelicGameType.Mines, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Mines!;
            var resultSection = $"🪂 Você saiu em **{MinesTableBuilder.FormatMultiplier(state.CurrentMultiplier)}** e resgatou "
                + $"**{EconomyFormat.Full(done.Outcome?.ReturnAmount ?? 0)}** moedas!\n"
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = MinesTableBuilder.BuildMinesTable(state, Context.User, payout.Balance, resultSection);
                m.Components = MinesTableBuilder.BuildMinesResultComponents(Context.User.Id, bet, state.MinesCount);
            });
        }

        [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task MinesReplayAsync(ulong ownerId, ulong bet, int mines)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Mines, Context.User.Id, bet,
                new BetOptions { MinesCount = mines },
                "🧨 Você já tem um campo aberto! Use os botões do campo existente.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Novo campo nas minas", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);
            _bets.SetState(Context.User.Id, opened.State);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = MinesTableBuilder.BuildMinesTable(opened.State.Mines!, Context.User, balance);
                m.Components = MinesTableBuilder.BuildMinesComponents(opened.State.Mines!, Context.User.Id);
            });
        }

        [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.PaytableAction, true)]
        public async Task MinesPaytableAsync()
            => await RespondAsync(embed: MinesTableBuilder.BuildMinesPaytable(), ephemeral: true);

        [ComponentInteraction(MinesTableBuilder.CustomIdPrefix + MinesTableBuilder.LeaveAction + ":*", true)]
        public async Task MinesLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Mines, Context.User.Id);
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
                "Minas expiradas", RelicGameType.Mines, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Mines, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static int ToMinesCount(MinesCountChoice choice) => (int)choice + 1;
    }
}

public enum MinesCountChoice
{
    [ChoiceDisplay("1 mina")]
    Uma,
    [ChoiceDisplay("2 minas")]
    Duas,
    [ChoiceDisplay("3 minas")]
    Tres,
    [ChoiceDisplay("4 minas")]
    Quatro,
    [ChoiceDisplay("5 minas")]
    Cinco,
    [ChoiceDisplay("6 minas")]
    Seis
}