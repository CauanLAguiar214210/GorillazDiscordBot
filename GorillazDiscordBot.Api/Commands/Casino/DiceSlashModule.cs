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
    public class DiceSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public DiceSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("dados", "Aposta nos dados com botões")]
        public async Task DiceAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("tipo", "Tipo de aposta")] DiceBetChoice tipo)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Dice, Context.User.Id, amount,
                new BetOptions { DiceBet = ToBetType(tipo) },
                "🎲 Você já tem uma mesa aberta! Use os botões da mesa existente.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta nos dados"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: DiceTableBuilder.BuildDiceTable(opened.State.Dice!, Context.User, balance),
                components: DiceTableBuilder.BuildDiceComponents());
        }

        [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.RollAction, true)]
        public async Task DiceRollAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Dice, Context.User.Id, "roll", null,
                "🎲 Esta mesa não tem dados ativos. Use `/cassino dados` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username, "Pagamento dos dados",
                RelicGameType.Dice, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Dice!;
            var resultSection = DescribeResult(state, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = DiceTableBuilder.BuildDiceTable(state, Context.User, payout.Balance, resultSection);
                m.Components = DiceTableBuilder.BuildDiceResultComponents(Context.User.Id, bet, state.BetType);
            });
        }

        [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task DiceReplayAsync(ulong ownerId, ulong bet, int type)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Dice, Context.User.Id, bet,
                new BetOptions { DiceBet = (DiceBetType)type },
                "🎲 Você já tem uma mesa aberta! Use os botões da mesa existente.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova rodada nos dados", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = DiceTableBuilder.BuildDiceTable(opened.State.Dice!, Context.User, balance);
                m.Components = DiceTableBuilder.BuildDiceComponents();
            });
        }

        [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.PaytableAction, true)]
        public async Task DicePaytableAsync()
            => await RespondAsync(embed: DiceTableBuilder.BuildDicePaytable(), ephemeral: true);

        [ComponentInteraction(DiceTableBuilder.CustomIdPrefix + DiceTableBuilder.LeaveAction + ":*", true)]
        public async Task DiceLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Dice, Context.User.Id);
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
                "Dados expirados", RelicGameType.Dice, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Dice, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.DiceState state, ulong returnAmount)
            => returnAmount > 0
                ? $"🎉 **{DiceTableBuilder.DescribeBetType(state.BetType)}** — você venceu! Recebeu "
                    + $"**{EconomyFormat.Full(returnAmount)}** moedas.\n"
                : $"😢 **{DiceTableBuilder.DescribeBetType(state.BetType)}** — você perdeu. Boa sorte na próxima!\n";

        private static DiceBetType ToBetType(DiceBetChoice choice) => choice switch
        {
            DiceBetChoice.Alta => DiceBetType.High,
            DiceBetChoice.Baixa => DiceBetType.Low,
            DiceBetChoice.Sete => DiceBetType.Seven,
            DiceBetChoice.Dupla => DiceBetType.Doubles,
            _ => DiceBetType.High
        };
    }
}

public enum DiceBetChoice
{
    [ChoiceDisplay("Alta (8–12)")]
    Alta,
    [ChoiceDisplay("Baixa (2–6)")]
    Baixa,
    [ChoiceDisplay("Sete (7)")]
    Sete,
    [ChoiceDisplay("Dupla (pares)")]
    Dupla
}