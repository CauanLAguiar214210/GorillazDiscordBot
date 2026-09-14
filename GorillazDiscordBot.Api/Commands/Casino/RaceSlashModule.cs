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
    public class RaceSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public RaceSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("corrida", "Aposta no cavalo que vai vencer a corrida")]
        public async Task RaceAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("cavalo", "O cavalo em que você quer apostar")] RacePickChoice cavalo)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Race, Context.User.Id, amount,
                new BetOptions { RacePick = (int)cavalo },
                "🏇 Você já tem uma corrida em andamento! Use os botões da corrida aberta.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta na corrida"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: RaceTableBuilder.BuildRaceTable(opened.State.Race!, Context.User, balance),
                components: RaceTableBuilder.BuildRaceComponents(Context.User.Id));
        }

        [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.StartAction, true)]
        public async Task RaceStartAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Race, Context.User.Id, "start", null,
                "🏇 Esta corrida não está mais ativa. Use `/cassino corrida` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Resultado da corrida", RelicGameType.Race, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Race!;
            var resultSection = DescribeResult(state, bet, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = RaceTableBuilder.BuildRaceTable(state, Context.User, payout.Balance, resultSection);
                m.Components = RaceTableBuilder.BuildRaceResultComponents(Context.User.Id, bet, state.PlayerPick);
            });
        }

        [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task RaceReplayAsync(ulong ownerId, ulong bet, int pick)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Race, Context.User.Id, bet,
                new BetOptions { RacePick = pick },
                "🏇 Você já tem uma corrida em andamento! Use os botões da corrida aberta.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova corrida", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = RaceTableBuilder.BuildRaceTable(opened.State.Race!, Context.User, balance);
                m.Components = RaceTableBuilder.BuildRaceComponents(Context.User.Id);
            });
        }

        [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.PaytableAction, true)]
        public async Task RacePaytableAsync()
            => await RespondAsync(embed: RaceTableBuilder.BuildRacePaytable(), ephemeral: true);

        [ComponentInteraction(RaceTableBuilder.CustomIdPrefix + RaceTableBuilder.LeaveAction + ":*", true)]
        public async Task RaceLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Race, Context.User.Id);
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
                "Corrida expirada", RelicGameType.Race, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Race, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.RaceState state, ulong bet, ulong returnAmount)
        {
            var winner = state.WinnerIndex ?? state.PlayerPick;
            return state.PlayerPick == state.WinnerIndex
                ? $"🏆 Seu **{RaceTableBuilder.DescribePick(winner)}** venceu e você faturou "
                    + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
                : $"❌ O vencedor foi **{RaceTableBuilder.DescribePick(winner)}**. "
                    + $"Perdeu **{EconomyFormat.Full(bet)}** moedas.\n";
        }
    }
}

public enum RacePickChoice
{
    [ChoiceDisplay("Cavalo 1 🐎")]
    Um,
    [ChoiceDisplay("Cavalo 2 🐴")]
    Dois,
    [ChoiceDisplay("Cavalo 3 🦄")]
    Tres,
    [ChoiceDisplay("Cavalo 4 🐎")]
    Quatro,
    [ChoiceDisplay("Cavalo 5 🐴")]
    Cinco,
    [ChoiceDisplay("Cavalo 6 🦄")]
    Seis
}