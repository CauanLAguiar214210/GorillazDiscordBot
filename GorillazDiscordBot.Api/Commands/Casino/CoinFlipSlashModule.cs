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
    public class CoinFlipSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public CoinFlipSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("caraoucoroa", "Aposta em cara ou coroa com botões")]
        public async Task CoinFlipAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("lado", "Lado da moeda")] CoinSideChoice lado)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Coin, Context.User.Id, amount,
                new BetOptions { CoinSide = ToSide(lado) },
                "🪙 Você já tem uma moeda no ar! Use os botões da mesa existente.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta em cara ou coroa"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: CoinTableBuilder.BuildCoinFlipTable(opened.State.Coin!, Context.User, balance),
                components: CoinTableBuilder.BuildCoinComponents());
        }

        [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.FlipAction, true)]
        public async Task CoinFlipActionAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Coin, Context.User.Id, "flip", null,
                "🪙 Esta mesa não tem uma moeda ativa. Use `/cassino caraoucoroa` para começar outra.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Pagamento de cara ou coroa", RelicGameType.Coin, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Coin!;
            var resultSection = DescribeResult(state, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = CoinTableBuilder.BuildCoinFlipTable(state, Context.User, payout.Balance, resultSection);
                m.Components = CoinTableBuilder.BuildCoinResultComponents(Context.User.Id, bet, state.Side);
            });
        }

        [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task CoinReplayAsync(ulong ownerId, ulong bet, int side)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Coin, Context.User.Id, bet,
                new BetOptions { CoinSide = (CoinSide)side },
                "🪙 Você já tem uma moeda no ar! Use os botões da mesa existente.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova rodada em cara ou coroa", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = CoinTableBuilder.BuildCoinFlipTable(opened.State.Coin!, Context.User, balance);
                m.Components = CoinTableBuilder.BuildCoinComponents();
            });
        }

        [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.PaytableAction, true)]
        public async Task CoinPaytableAsync()
            => await RespondAsync(embed: CoinTableBuilder.BuildCoinPaytable(), ephemeral: true);

        [ComponentInteraction(CoinTableBuilder.CustomIdPrefix + CoinTableBuilder.LeaveAction + ":*", true)]
        public async Task CoinLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Coin, Context.User.Id);
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
                "Cara ou coroa expirada", RelicGameType.Coin, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Coin, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.CoinState state, ulong returnAmount)
            => returnAmount > 0
                ? $"🎉 **{CoinTableBuilder.DescribeSide(state.Side)}** — você venceu! Recebeu "
                    + $"**{EconomyFormat.Full(returnAmount)}** moedas.\n"
                : $"😢 Caiu **{CoinTableBuilder.DescribeSide(state.Result!.Value)}** — você perdeu. Boa sorte na próxima!\n";

        private static CoinSide ToSide(CoinSideChoice choice) => choice switch
        {
            CoinSideChoice.Cara => CoinSide.Cara,
            CoinSideChoice.Coroa => CoinSide.Coroa,
            _ => CoinSide.Cara
        };
    }
}

public enum CoinSideChoice
{
    [ChoiceDisplay("Cara")]
    Cara,
    [ChoiceDisplay("Coroa")]
    Coroa
}