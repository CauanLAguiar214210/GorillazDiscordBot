using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Bets;
using LuckyMonkey.Contracts.Common;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.Sessions;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Commands.Casino;

[Group("cassino", "Jogos de cassino")]
public partial class CasinoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoApiClient _casino;
    private readonly PayoutService _play;
    private readonly CasinoBetTracker _bets;

    public CasinoSlashModule(CasinoApiClient casino, PayoutService play, CasinoBetTracker bets)
    {
        _casino = casino;
        _play = play;
        _bets = bets;
    }

    [SlashCommand("roleta", "Aposta na roleta com botões")]
    public async Task RouletteAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor,
        [Summary("tipo", "Tipo de aposta")] RouletteBetChoice tipo = RouletteBetChoice.Numero,
        [Summary("alvo", "Número de 0 a 36 (só para tipo número)")] int? alvo = null)
    {
        if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
        {
            await RespondAsync(parseError, ephemeral: true);
            return;
        }

        var (betType, target, error) = ResolveChoice(tipo, alvo);
        if (error != null)
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Roulette, Context.User.Id, amount,
            new BetOptions { RouletteBet = betType, RouletteTarget = target },
            "🎰 Você já tem uma roleta em andamento! Use os botões da mesa aberta.");
        if (opened == null)
            return;

        await SettleExpiredAsync(opened.ExpiredSettle, RelicGameType.Roulette);

        if (!await DeductOrLeaveAsync(amount, GameKind.Roulette, "Aposta na roleta"))
            return;

        _bets.Set(Context.User.Id, amount);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        var state = opened.State.Roulette!;

        await RespondAsync(
            embed: CasinoTableBuilder.BuildRouletteTable(state, Context.User, balance),
            components: CasinoTableBuilder.BuildRouletteComponents(state.Bets.Count > 0));
    }

    [SlashCommand("cacaniquel", "Joga na caça-níquel com botões")]
    public async Task SlotAsync(
        [Summary("valor", "Quantidade de moedas para apostar. Ex: 100, 1k, 1M")] string valor)
    {
        if (!EconomyAmountParser.TryParse(valor, out var amount, out var parseError))
        {
            await RespondAsync(parseError, ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Slots, Context.User.Id, amount, null,
            "🎰 Você já tem uma máquina em andamento! Use o botão da máquina aberta.");
        if (opened == null)
            return;

        await SettleExpiredAsync(opened.ExpiredSettle, RelicGameType.Slots);

        if (!await DeductOrLeaveAsync(amount, GameKind.Slots, "Aposta na caça-níquel"))
            return;

        _bets.Set(Context.User.Id, amount);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        var state = opened.State.Slots!;

        await RespondAsync(
            embed: CasinoTableBuilder.BuildSlotTable(state, amount, Context.User, balance),
            components: CasinoTableBuilder.BuildSlotComponents(state.HasSpun));
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulSpinAction, true)]
    public async Task RouletteSpinAsync()
    {
        await DeferAsync();

        var action = await CasinoApiFlow.RunActionAsync(
            this, _casino, GameKind.Roulette, Context.User.Id, "spin", null,
            "🎰 Esta mesa não tem uma roleta ativa. Use `/cassino roleta` para começar outra.");
        if (action == null)
            return;

        var payout = await _play.PayOutAsync(
            Context.User.Id, action.Outcome?.ReturnAmount ?? 0, Context.User.Username, "Pagamento da roleta",
            RelicGameType.Roulette, action.State.Roulette?.TotalBet ?? _bets.Get(Context.User.Id));
        _bets.Remove(Context.User.Id);

        var state = action.State.Roulette!;
        var firstBet = state.Bets.FirstOrDefault();
        var resultSection = DescribeRouletteResult(state)
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildRouletteTable(state, Context.User, payout.Balance, resultSection);
            m.Components = firstBet == null
                ? new ComponentBuilder().Build()
                : CasinoTableBuilder.BuildRouletteReplayComponents(
                    Context.User.Id, firstBet.Amount, firstBet.Type, firstBet.Target);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulAddBetAction, true)]
    public async Task RouletteAddBetAsync()
        => await RespondWithModalAsync<AddBetModal>("roul:addbet:modal");

    [ModalInteraction("roul:addbet:modal")]
    public async Task RouletteAddBetModalAsync(AddBetModal modal)
    {
        await DeferAsync();

        if (!EconomyAmountParser.TryParse(modal.Valor, out var amount, out var parseError))
        {
            await FollowupAsync(parseError, ephemeral: true);
            return;
        }

        var (betType, target, error) = ParseModalBet(modal);
        if (error != null)
        {
            await FollowupAsync(error, ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, "Aposta extra na roleta");

        if (!deducted)
        {
            await FollowupAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        StakeResponse stake;
        try
        {
            stake = await _casino.AddStakeAsync(GameKind.Roulette, Context.User.Id,
                new AddStakeRequest { Amount = amount, BetType = betType, Target = target });
        }
        catch (CasinoApiException ex)
        {
            await _play.RefundAsync(
                Context.User.Id, amount, Context.User.Username, "Reembolso de aposta extra na roleta");
            await FollowupAsync(ex.Message, ephemeral: true);
            return;
        }

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildRouletteTable(stake.State.Roulette!, Context.User, balance);
            m.Components = CasinoTableBuilder.BuildRouletteComponents(stake.State.Roulette!.Bets.Count > 0);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.SlotCustomIdPrefix + CasinoTableBuilder.SlotSpinAction, true)]
    public async Task SlotSpinAsync()
    {
        await DeferAsync();

        var action = await CasinoApiFlow.RunActionAsync(
            this, _casino, GameKind.Slots, Context.User.Id, "spin", null,
            "🎰 Esta máquina não tem um jogo ativo. Use `/cassino cacaniquel` para começar outro.");
        if (action == null)
            return;

        var bet = _bets.Get(Context.User.Id);
        var returnAmount = action.Outcome?.ReturnAmount ?? 0;
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento da caça-níquel",
            RelicGameType.Slots, bet);
        _bets.Remove(Context.User.Id);

        var state = action.State.Slots!;
        var resultSection = (returnAmount > 0
            ? $"🎉 **Você ganhou!** Recebeu **{EconomyFormat.Full(returnAmount)}** moedas."
            : "😢 Você não acertou nenhuma combinação. Boa sorte na próxima!")
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildSlotTable(state, bet, Context.User, payout.Balance, resultSection);
            m.Components = CasinoTableBuilder.BuildSlotReplayComponents(Context.User.Id, bet);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.SlotCustomIdPrefix + CasinoTableBuilder.SlotReplayAction + ":*:*", true)]
    public async Task SlotReplayAsync(ulong ownerId, ulong bet)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Slots, Context.User.Id, bet, null,
            "🎰 Você já tem uma máquina em andamento! Use o botão da máquina aberta.", followup: true);
        if (opened == null)
            return;

        await SettleExpiredAsync(opened.ExpiredSettle, RelicGameType.Slots);

        if (!await DeductOrLeaveAsync(bet, GameKind.Slots, "Nova rodada na caça-níquel", followup: true))
            return;

        _bets.Set(Context.User.Id, bet);

        var spin = await CasinoApiFlow.RunActionAsync(
            this, _casino, GameKind.Slots, Context.User.Id, "spin", null,
            "🎰 Esta máquina não tem um jogo ativo. Use `/cassino cacaniquel` para começar outro.");
        if (spin == null)
            return;

        var returnAmount = spin.Outcome?.ReturnAmount ?? 0;
        var payout = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento da caça-níquel",
            RelicGameType.Slots, bet);
        _bets.Remove(Context.User.Id);

        var resultSection = (returnAmount > 0
            ? $"🎉 **Você ganhou!** Recebeu **{EconomyFormat.Full(returnAmount)}** moedas."
            : "😢 Você não acertou nenhuma combinação. Boa sorte na próxima!")
            + CasinoTableBuilder.DescribeAppliedRelic(payout);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildSlotTable(spin.State.Slots!, bet, Context.User, payout.Balance, resultSection);
            m.Components = CasinoTableBuilder.BuildSlotReplayComponents(ownerId, bet);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulReplayAction + ":*:*:*:*", true)]
    public async Task RouletteReplayAsync(ulong ownerId, ulong bet, int type, int target)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        var opened = await CasinoApiFlow.OpenBetAsync(
            this, _casino, _play, _bets, GameKind.Roulette, Context.User.Id, bet,
            new BetOptions { RouletteBet = (RouletteBetType)type, RouletteTarget = target },
            "🎰 Você já tem uma roleta em andamento! Use os botões da mesa aberta.", followup: true);
        if (opened == null)
            return;

        await SettleExpiredAsync(opened.ExpiredSettle, RelicGameType.Roulette);

        if (!await DeductOrLeaveAsync(bet, GameKind.Roulette, "Nova rodada na roleta", followup: true))
            return;

        _bets.Set(Context.User.Id, bet);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);
        var state = opened.State.Roulette!;

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildRouletteTable(state, Context.User, balance);
            m.Components = CasinoTableBuilder.BuildRouletteComponents(state.Bets.Count > 0);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulPaytableAction, true)]
    public async Task RoulettePaytableAsync()
        => await RespondAsync(embed: CasinoTableBuilder.BuildRoulettePaytable(), ephemeral: true);

    [ComponentInteraction(CasinoTableBuilder.SlotCustomIdPrefix + CasinoTableBuilder.SlotPaytableAction, true)]
    public async Task SlotPaytableAsync()
        => await RespondAsync(embed: CasinoTableBuilder.BuildSlotPaytable(), ephemeral: true);

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulLeaveAction + ":*", true)]
    public async Task RouletteLeaveAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Roulette, Context.User.Id);
        _bets.Remove(Context.User.Id);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Components = new ComponentBuilder().Build();
        });
    }

    [ComponentInteraction(CasinoTableBuilder.SlotCustomIdPrefix + CasinoTableBuilder.SlotLeaveAction + ":*", true)]
    public async Task SlotLeaveAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta partida não é sua.", ephemeral: true);
            return;
        }

        await CasinoApiFlow.LeaveAsync(_casino, GameKind.Slots, Context.User.Id);
        _bets.Remove(Context.User.Id);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task SettleExpiredAsync(Outcome? expired, RelicGameType gameType)
    {
        if (expired is { } outcome)
        {
            await _play.PayOutAsync(
                Context.User.Id, outcome.ReturnAmount, Context.User.Username,
                $"{gameType} expirado(a)", gameType, _bets.Get(Context.User.Id));
        }
    }

    private async Task<bool> DeductOrLeaveAsync(
        ulong amount, GameKind game, string description, bool followup = false)
    {
        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, amount, Context.User.Username, description);

        if (deducted)
            return true;

        await CasinoApiFlow.LeaveAsync(_casino, game, Context.User.Id);

        var message = "❌ Você não tem moedas suficientes na carteira.";
        if (followup)
            await FollowupAsync(message, ephemeral: true);
        else
            await RespondAsync(message, ephemeral: true);

        return false;
    }

    private static string DescribeRouletteResult(RouletteState state)
    {
        var sb = new System.Text.StringBuilder();
        if (state.ResultNumber is not { } result)
            return sb.ToString();

        foreach (var bet in state.Bets)
        {
            var win = BetReturn(bet, result);
            if (win > 0)
                sb.AppendLine($"✅ {DescribeBet(bet)} — venceu **{EconomyFormat.Full(win)}**");
            else
                sb.AppendLine($"❌ {DescribeBet(bet)} — perdeu **{EconomyFormat.Full(bet.Amount)}**");
        }
        return sb.ToString();
    }

    private static string DescribeBet(RouletteBetState bet)
        => $"{EconomyFormat.Full(bet.Amount)} moedas em {bet.Type} ({bet.Target})";

    private static ulong BetReturn(RouletteBetState bet, int resultNumber)
    {
        var isWin = bet.Type switch
        {
            RouletteBetType.Number => resultNumber == bet.Target,
            RouletteBetType.Color => resultNumber != 0
                && RouletteRules.ColorOf(resultNumber) == (RouletteColor)bet.Target,
            RouletteBetType.Parity => resultNumber != 0 && resultNumber % 2 == bet.Target,
            RouletteBetType.Half => bet.Target == 0 ? resultNumber is >= 1 and <= 18 : resultNumber >= 19,
            _ => false
        };

        return !isWin
            ? 0
            : bet.Type == RouletteBetType.Number
                ? bet.Amount * (ulong)RouletteRules.StraightPayout
                : (ulong)Math.Floor(bet.Amount * RouletteRules.EvenMoneyPayout);
    }

    private static (RouletteBetType type, int target, string? error) ParseModalBet(AddBetModal modal)
    {
        var typeKeyword = modal.Tipo?.Trim().ToLowerInvariant() ?? "numero";
        var target = modal.Alvo?.Trim() ?? "0";

        switch (typeKeyword)
        {
            case "numero":
            case "number":
                if (!int.TryParse(target, out var num) || num < 0 || num > 36)
                    return (RouletteBetType.Number, 0, "⚠️ O número deve estar entre 0 e 36.");
                return (RouletteBetType.Number, num, null);
            case "vermelho":
            case "red":
                return (RouletteBetType.Color, (int)RouletteColor.Red, null);
            case "preto":
            case "black":
                return (RouletteBetType.Color, (int)RouletteColor.Black, null);
            case "par":
                return (RouletteBetType.Parity, 0, null);
            case "impar":
                return (RouletteBetType.Parity, 1, null);
            case "baixa":
                return (RouletteBetType.Half, 0, null);
            case "alta":
                return (RouletteBetType.Half, 1, null);
            default:
                return (RouletteBetType.Number, 0, "⚠️ Tipo inválido.");
        }
    }

    private static (RouletteBetType type, int target, string? error) ResolveChoice(
        RouletteBetChoice choice, int? alvo)
    {
        return choice switch
        {
            RouletteBetChoice.Numero => alvo is { } n && n >= 0 && n <= 36
                ? (RouletteBetType.Number, n, (string?)null)
                : (RouletteBetType.Number, 0, "⚠️ Para tipo número, informe um alvo entre 0 e 36."),
            RouletteBetChoice.Vermelho => (RouletteBetType.Color, (int)RouletteColor.Red, (string?)null),
            RouletteBetChoice.Preto => (RouletteBetType.Color, (int)RouletteColor.Black, (string?)null),
            RouletteBetChoice.Par => (RouletteBetType.Parity, 0, (string?)null),
            RouletteBetChoice.Impar => (RouletteBetType.Parity, 1, (string?)null),
            RouletteBetChoice.Baixa => (RouletteBetType.Half, 0, (string?)null),
            RouletteBetChoice.Alta => (RouletteBetType.Half, 1, (string?)null),
            _ => (RouletteBetType.Number, 0, (string?)null)
        };
    }
}

public enum RouletteBetChoice
{
    [ChoiceDisplay("Número")]
    Numero,
    [ChoiceDisplay("Vermelho")]
    Vermelho,
    [ChoiceDisplay("Preto")]
    Preto,
    [ChoiceDisplay("Par")]
    Par,
    [ChoiceDisplay("Ímpar")]
    Impar,
    [ChoiceDisplay("Baixa (1-18)")]
    Baixa,
    [ChoiceDisplay("Alta (19-36)")]
    Alta
}

public class AddBetModal : IModal
{
    public string Title => "Adicionar aposta na roleta";

    [InputLabel("Valor")]
    [ModalTextInput("addbet_valor", TextInputStyle.Short, placeholder: "100, 1k, 1M, 1B")]
    public string Valor { get; set; } = string.Empty;

    [InputLabel("Tipo")]
    [ModalTextInput("addbet_tipo", TextInputStyle.Short, placeholder: "numero | vermelho | preto | par | impar | baixa | alta")]
    public string Tipo { get; set; } = string.Empty;

    [InputLabel("Alvo (número para tipo número)")]
    [ModalTextInput("addbet_alvo", TextInputStyle.Short, placeholder: "7")]
    public string Alvo { get; set; } = string.Empty;
}