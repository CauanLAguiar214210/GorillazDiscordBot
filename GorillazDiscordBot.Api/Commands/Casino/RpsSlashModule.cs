using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Api.Utils.TableBuilder.Cassino;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using LuckyMonkey.Contracts.Bets;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;

namespace GorillazDiscordBot.Commands.Casino;

public partial class CasinoSlashModule
{
    public class RpsSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly IWalletService _play;
        private readonly CasinoBetTracker _bets;

        public RpsSlashModule(CasinoApiClient casino, IWalletService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("jokenpo", "Pedra, papel e tesoura valendo moedas")]
        public async Task RpsAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("jogada", "Sua jogada")] RpsMoveChoice jogada)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Rps, Context.User.Id, amount,
                new BetOptions { RpsMove = (RpsMove)jogada },
                "🤚 Você já tem um jokenpô em andamento! Use os botões do jogo aberto.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no jokenpô"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: RpsTableBuilder.BuildRpsTable(opened.State.Rps!, Context.User, balance),
                components: RpsTableBuilder.BuildRpsComponents());
        }

        [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.PlayAction, true)]
        public async Task RpsPlayAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Rps, Context.User.Id, "play", null,
                "🤚 Este jokenpô não está mais ativo. Use `/cassino jokenpo` para começar outro.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var payout = await _play.PayOutAsync(
                Context.User.Id, done.Outcome?.ReturnAmount ?? 0, Context.User.Username,
                "Resultado do jokenpô", RelicGameType.Rps, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Rps!;
            var resultSection = DescribeResult(state, bet, done.Outcome?.ReturnAmount ?? 0)
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = RpsTableBuilder.BuildRpsTable(state, Context.User, payout.Balance, resultSection);
                m.Components = RpsTableBuilder.BuildRpsResultComponents(Context.User.Id, bet, (int)state.PlayerMove);
            });
        }

        [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task RpsReplayAsync(ulong ownerId, ulong bet, int move)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Rps, Context.User.Id, bet,
                new BetOptions { RpsMove = (RpsMove)move },
                "🤚 Você já tem um jokenpô em andamento! Use os botões do jogo aberto.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Nova rodada de jokenpô", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = RpsTableBuilder.BuildRpsTable(opened.State.Rps!, Context.User, balance);
                m.Components = RpsTableBuilder.BuildRpsComponents();
            });
        }

        [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.PaytableAction, true)]
        public async Task RpsPaytableAsync()
            => await RespondAsync(embed: RpsTableBuilder.BuildRpsPaytable(), ephemeral: true);

        [ComponentInteraction(RpsTableBuilder.CustomIdPrefix + RpsTableBuilder.LeaveAction + ":*", true)]
        public async Task RpsLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Rps, Context.User.Id);
            _bets.Remove(Context.User.Id);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Components = new ComponentBuilder().Build();
            });
        }

        private Task SettleExpiredAsync(Outcome expired)
            => CasinoApiFlow.SettleExpiredAsync(this, _play, _bets, Context.User.Id, expired, GameKind.Rps);

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Rps, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }

        private static string DescribeResult(LuckyMonkey.Contracts.State.RpsState state, ulong bet, ulong returnAmount)
            => state.Outcome switch
            {
                RpsOutcome.PlayerWin =>
                    $"🏆 **Vitória!** Oponente jogou **{RpsTableBuilder.DescribeMove(state.OpponentMove!.Value)}** "
                        + $"e você faturou **{EconomyFormat.Full(returnAmount)}** moedas!\n",
                RpsOutcome.Draw =>
                    $"🤝 **Empate!** Oponente também jogou **{RpsTableBuilder.DescribeMove(state.OpponentMove!.Value)}**. "
                        + $"A aposta de **{EconomyFormat.Full(bet)}** foi devolvida.\n",
                _ =>
                    $"❌ **Derrota.** Oponente jogou **{RpsTableBuilder.DescribeMove(state.OpponentMove ?? state.PlayerMove)}**. "
                        + $"Perdeu **{EconomyFormat.Full(bet)}** moedas.\n"
            };
    }
}

public enum RpsMoveChoice
{
    [ChoiceDisplay("Pedra ✊")]
    Pedra,
    [ChoiceDisplay("Papel ✋")]
    Papel,
    [ChoiceDisplay("Tesoura ✌️")]
    Tesoura
}