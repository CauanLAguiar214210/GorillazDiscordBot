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
    public class LimboSlashModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly CasinoApiClient _casino;
        private readonly PayoutService _play;
        private readonly CasinoBetTracker _bets;

        public LimboSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
        {
            _casino = casino;
            _play = play;
            _bets = bets;
        }

        [SlashCommand("limbo", "O número sorteado que passa do alvo multiplica a aposta")]
        public async Task LimboAsync(
            [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
            [Summary("alvo", "Multiplicador alvo que você quer alcançar")] LimboTargetChoice alvo = LimboTargetChoice.DuasVezes)
        {
            if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
            {
                await RespondAsync(parseError, ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Limbo, Context.User.Id, amount,
                new BetOptions { LimboTarget = ((int)alvo) / 100d },
                "🔮 Você já tem um limbo em andamento! Use os botões do jogo aberto.");
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(amount, "Aposta no limbo"))
                return;

            _bets.Set(Context.User.Id, amount);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await RespondAsync(
                embed: LimboTableBuilder.BuildLimboTable(opened.State.Limbo!, Context.User, balance),
                components: LimboTableBuilder.BuildLimboComponents());
        }

        [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.RevealAction, true)]
        public async Task LimboRevealAsync()
        {
            await DeferAsync();

            var done = await CasinoApiFlow.RunActionAsync(
                this, _casino, GameKind.Limbo, Context.User.Id, "roll", null,
                "🔮 Este limbo não está mais ativo. Use `/cassino limbo` para começar outro.");
            if (done == null)
                return;

            var bet = _bets.Get(Context.User.Id);
            var returnAmount = done.Outcome?.ReturnAmount ?? 0;
            var payout = await _play.PayOutAsync(
                Context.User.Id, returnAmount, Context.User.Username,
                returnAmount > 0 ? "Ganhou no limbo" : "Perdeu no limbo",
                RelicGameType.Limbo, bet);
            _bets.Remove(Context.User.Id);

            var state = done.State.Limbo!;
            var isWin = returnAmount > 0;
            var resultSection = (isWin
                    ? $"✅ O sorteado **{LimboTableBuilder.FormatMultiplier(state.Result!.Value)}** passou de "
                        + $"**{LimboTableBuilder.FormatMultiplier(state.Target)}** e você ganhou "
                        + $"**{EconomyFormat.Full(returnAmount)}** moedas!\n"
                    : $"❌ O sorteado **{LimboTableBuilder.FormatMultiplier(state.Result!.Value)}** ficou abaixo de "
                        + $"**{LimboTableBuilder.FormatMultiplier(state.Target)}**. Perdeu "
                        + $"**{EconomyFormat.Full(bet)}** moedas.\n")
                + CasinoTableBuilder.DescribeAppliedRelic(payout);

            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = LimboTableBuilder.BuildLimboTable(state, Context.User, payout.Balance, resultSection);
                m.Components = LimboTableBuilder.BuildLimboResultComponents(
                    Context.User.Id, bet, (int)(state.Target * 100));
            });
        }

        [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.ReplayAction + ":*:*:*", true)]
        public async Task LimboReplayAsync(ulong ownerId, ulong bet, int targetCents)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            var opened = await CasinoApiFlow.OpenBetAsync(
                this, _casino, _play, _bets, GameKind.Limbo, Context.User.Id, bet,
                new BetOptions { LimboTarget = targetCents / 100d },
                "🔮 Você já tem um limbo em andamento! Use os botões do jogo aberto.", followup: true);
            if (opened == null)
                return;

            if (opened.ExpiredSettle is { } expired)
                await SettleExpiredAsync(expired);

            if (!await DeductOrLeaveAsync(bet, "Novo limbo", followup: true))
                return;

            _bets.Set(Context.User.Id, bet);

            var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = LimboTableBuilder.BuildLimboTable(opened.State.Limbo!, Context.User, balance);
                m.Components = LimboTableBuilder.BuildLimboComponents();
            });
        }

        [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.PaytableAction, true)]
        public async Task LimboPaytableAsync()
            => await RespondAsync(embed: LimboTableBuilder.BuildLimboPaytable(), ephemeral: true);

        [ComponentInteraction(LimboTableBuilder.CustomIdPrefix + LimboTableBuilder.LeaveAction + ":*", true)]
        public async Task LimboLeaveAsync(ulong ownerId)
        {
            await DeferAsync();

            if (ownerId != Context.User.Id)
            {
                await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
                return;
            }

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Limbo, Context.User.Id);
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
                "Limbo expirado", RelicGameType.Limbo, _bets.Get(Context.User.Id));
        }

        private async Task<bool> DeductOrLeaveAsync(ulong amount, string description, bool followup = false)
        {
            var (deducted, _) = await _play.DeductBetAsync(
                Context.User.Id, amount, Context.User.Username, description);

            if (deducted)
                return true;

            await CasinoApiFlow.LeaveAsync(_casino, GameKind.Limbo, Context.User.Id);

            var message = "❌ Você não tem moedas suficientes na carteira.";
            if (followup)
                await FollowupAsync(message, ephemeral: true);
            else
                await RespondAsync(message, ephemeral: true);

            return false;
        }
    }
}

public enum LimboTargetChoice
{
    [ChoiceDisplay("2x")]
    DuasVezes = 200,
    [ChoiceDisplay("3x")]
    TresVezes = 300,
    [ChoiceDisplay("5x")]
    CincoVezes = 500,
    [ChoiceDisplay("10x")]
    DezVezes = 1000
}