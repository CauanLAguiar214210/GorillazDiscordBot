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
    public class BaccaratSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public BaccaratSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("baccarat", "Aposte no jogador, no banco ou no empate")]
        public async Task BaccaratAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("aposta", "Em quem você quer apostar")] BaccaratBetChoice aposta)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Baccarat, Context.User.Id, amount,
                new BetOptions { BaccaratBet = ToBetType(aposta) },
                "🎴 Você já tem uma partida em andamento! Use os botões da mesa aberta.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no baccarat"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: BaccaratTableBuilder.BuildBaccaratTable(opened.State.Baccarat!, Context.User, balance),
                components: BaccaratTableBuilder.BuildBaccaratComponents(Context.User.Id));
        }

        [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.RevealAction, true)]
        public async Task BaccaratRevealAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Baccarat, Context.User.Id, "reveal", null,
                "🎴 Esta mesa não está mais ativa. Use `/cassino baccarat` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Resultado do baccarat", RelicGameType.Baccarat, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Baccarat!;
            var resultSection = DescribeResult(state, bet, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BaccaratTableBuilder.BuildBaccaratTable(state, Context.User, payout.Balance, resultSection);
                m.Components = BaccaratTableBuilder.BuildBaccaratResultComponents(Context.User.Id, bet, (int)state.BetType);
            });
        }

        [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task BaccaratReplayAsync(ulong ownerId, ulong bet, int betType)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Baccarat, Context.User.Id, bet,
                new BetOptions { BaccaratBet = (BaccaratBetType)betType },
                "🎴 Você já tem uma partida em andamento! Use os botões da mesa aberta.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova mão de baccarat", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BaccaratTableBuilder.BuildBaccaratTable(opened.State.Baccarat!, Context.User, balance);
                m.Components = BaccaratTableBuilder.BuildBaccaratComponents(Context.User.Id);
            });
        }

        [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.PaytableAction, true)]
        public async Task BaccaratPaytableAsync()
            => await RespondAsync(embed: BaccaratTableBuilder.BuildBaccaratPaytable(), ephemeral: true);

        [ComponentInteraction(BaccaratTableBuilder.CustomIdPrefix + BaccaratTableBuilder.LeaveAction + ":*", true)]
        public async Task BaccaratLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Baccarat, Context.User.Id);
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
                "Baccarat expirado", RelicGameType.Baccarat, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Baccarat, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.BaccaratState state, ulong bet, ulong returnAmount)
        {
            if (state.Outcome == BaccaratOutcome.Tie)
                return $"🤝 **Empate!** Jogador e banco têm **{state.PlayerValue}**. "
                    + $"Você recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n";

            var winner = state.Outcome == BaccaratOutcome.PlayerWin ? "Jogador" : "Banco";

            return returnAmount > 0
                ? $"🎉 **{winner} venceu!** Você recebeu **{EconomyFormat.Full(returnAmount)}** moedas.\n"
                : $"😢 **{winner} venceu.** Você apostou no outro lado e perdeu "
                    + $"**{EconomyFormat.Full(bet)}** moedas.\n";
        }

        private static BaccaratBetType ToBetType(BaccaratBetChoice choice) => choice switch
        {
            BaccaratBetChoice.Jogador => BaccaratBetType.Player,
            BaccaratBetChoice.Banco => BaccaratBetType.Banker,
            BaccaratBetChoice.Empate => BaccaratBetType.Tie,
            _ => BaccaratBetType.Player
        };
    }
}

public enum BaccaratBetChoice
{
    [ChoiceDisplay("Jogador ✋ (2x)")]
    Jogador,
    [ChoiceDisplay("Banco 🏦 (1.95x)")]
    Banco,
    [ChoiceDisplay("Empate 🤝 (9x)")]
    Empate
}