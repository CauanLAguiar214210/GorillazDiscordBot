using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Casino;

public class CasinoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CasinoPlayService _play;
    private readonly CasinoSessionManager _sessions;

    public CasinoSlashModule(CasinoPlayService play, CasinoSessionManager sessions)
    {
        _play = play;
        _sessions = sessions;
    }

    [SlashCommand("roleta", "Aposta na roleta com botões")]
    public async Task RouletteAsync(
        [Summary("valor", "Quantidade de moedas para apostar")] int valor,
        [Summary("tipo", "Tipo de aposta")] RouletteBetChoice tipo = RouletteBetChoice.Numero,
        [Summary("alvo", "Número de 0 a 36 (só para tipo número)")] int? alvo = null)
    {
        var (betType, target, error) = ResolveChoice(tipo, alvo);
        if (error != null)
        {
            await RespondAsync(error, ephemeral: true);
            return;
        }
        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await RespondAsync("🎰 Você já tem uma roleta em andamento! Use os botões da mesa aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, valor, Context.User.Username, "Aposta na roleta");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var game = new RouletteGame();
        game.AddBet(valor, betType, target);

        var session = CasinoSession.ForRoulette(valor, game);
        _sessions.Add(Context.User.Id, session);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: CasinoTableBuilder.BuildRouletteTable(game, Context.User, balance),
            components: CasinoTableBuilder.BuildRouletteComponents(game.Bets.Count > 0));
    }

    [SlashCommand("cacaniquel", "Joga na caça-níquel com botões")]
    public async Task SlotAsync(
        [Summary("valor", "Quantidade de moedas para apostar")] int valor)
    {
        var expired = _sessions.TakeExpired(Context.User.Id);
        if (expired != null)
            await SettleExpiredAsync(expired);

        if (_sessions.GetActive(Context.User.Id) != null)
        {
            await RespondAsync("🎰 Você já tem uma máquina em andamento! Use o botão da máquina aberta.", ephemeral: true);
            return;
        }

        var (deducted, _) = await _play.DeductBetAsync(
            Context.User.Id, valor, Context.User.Username, "Aposta na caça-níquel");

        if (!deducted)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira.", ephemeral: true);
            return;
        }

        var slots = new SlotMachineGame();
        var session = CasinoSession.ForSlots(valor, slots);
        _sessions.Add(Context.User.Id, session);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await RespondAsync(
            embed: CasinoTableBuilder.BuildSlotTable(slots, valor, Context.User, balance),
            components: CasinoTableBuilder.BuildSlotComponents(slots.HasSpun));
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulSpinAction, true)]
    public async Task RouletteSpinAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Roulette == null)
        {
            await FollowupAsync("🎰 Esta mesa não tem uma roleta ativa. Use `/roleta` para começar outra.", ephemeral: true);
            return;
        }

        var game = session.Roulette;
        game.Spin();
        _sessions.Remove(Context.User.Id);

        var balance = await _play.PayOutAsync(
            Context.User.Id, game.CalculateTotalReturn(), Context.User.Username, "Pagamento da roleta");

        var resultSection = DescribeRouletteResult(game);
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildRouletteTable(game, Context.User, balance, resultSection);
            m.Components = new ComponentBuilder().Build();
        });
    }

    [ComponentInteraction(CasinoTableBuilder.RoulCustomIdPrefix + CasinoTableBuilder.RoulAddBetAction, true)]
    public async Task RouletteAddBetAsync()
    {
        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Roulette == null)
        {
            await RespondAsync("🎰 Esta mesa não tem uma roleta ativa. Use `/roleta` para começar outra.",
                ephemeral: true, components: new ComponentBuilder().Build());
            return;
        }

        await RespondWithModalAsync<AddBetModal>("roul:addbet:modal");
    }

    [ModalInteraction("roul:addbet:modal")]
    public async Task RouletteAddBetModalAsync(AddBetModal modal)
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Roulette == null)
        {
            await FollowupAsync("🎰 O tempo para adicionar aposta expirou. Use `/roleta` novamente.", ephemeral: true);
            return;
        }

        if (!int.TryParse(modal.Valor, out var amount) || amount <= 0)
        {
            await FollowupAsync("⚠️ Informe um valor numérico positivo.", ephemeral: true);
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

        session.Roulette.AddBet(amount, betType, target);
        _sessions.Touch(Context.User.Id);

        var balance = await _play.GetBalanceAsync(Context.User.Id, Context.User.Username);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildRouletteTable(session.Roulette, Context.User, balance);
            m.Components = CasinoTableBuilder.BuildRouletteComponents(session.Roulette.Bets.Count > 0);
        });
    }

    [ComponentInteraction(CasinoTableBuilder.SlotCustomIdPrefix + CasinoTableBuilder.SlotSpinAction, true)]
    public async Task SlotSpinAsync()
    {
        await DeferAsync();

        var session = _sessions.GetActive(Context.User.Id);
        if (session?.Slots == null)
        {
            await FollowupAsync("🎰 Esta máquina não tem um jogo ativo. Use `/cacaniquel` para começar outro.", ephemeral: true);
            return;
        }

        var slots = session.Slots;
        slots.Spin();
        _sessions.Remove(Context.User.Id);

        var returnAmount = SlotMachineGame.CalculateReturn(session.Bet, slots.Reels);
        var balance = await _play.PayOutAsync(
            Context.User.Id, returnAmount, Context.User.Username, "Pagamento da caça-níquel");

        var resultSection = returnAmount > 0
            ? $"🎉 **Você ganhou!** Recebeu **{returnAmount}** moedas."
            : "😢 Você não acertou nenhuma combinação. Boa sorte na próxima!";

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = CasinoTableBuilder.BuildSlotTable(slots, session.Bet, Context.User, balance, resultSection);
            m.Components = new ComponentBuilder().Build();
        });
    }

    private async Task SettleExpiredAsync(CasinoSession expired)
    {
        if (expired.Roulette is { } roulette && roulette.Bets.Count > 0)
        {
            roulette.Spin();
            await _play.PayOutAsync(
                Context.User.Id, roulette.CalculateTotalReturn(), Context.User.Username, "Roleta expirada");
        }
        else if (expired.Slots is { } slots && !slots.HasSpun)
        {
            slots.Spin();
            await _play.PayOutAsync(
                Context.User.Id, SlotMachineGame.CalculateReturn(expired.Bet, slots.Reels),
                Context.User.Username, "Caça-níquel expirada");
        }
    }

    private static string DescribeRouletteResult(RouletteGame game)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var bet in game.Bets)
        {
            var win = game.CalculateReturn(bet);
            if (win > 0)
                sb.AppendLine($"✅ {DescribeBet(bet)} — venceu **{win}**");
            else
                sb.AppendLine($"❌ {DescribeBet(bet)} — perdeu **{bet.Amount}**");
        }
        return sb.ToString();
    }

    private static string DescribeBet(RouletteBet bet)
    {
        return $"{bet.Amount} moedas em {bet.Type} ({bet.Target})";
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
    [ModalTextInput("addbet_valor", TextInputStyle.Short, placeholder: "100")]
    public string Valor { get; set; } = string.Empty;

    [InputLabel("Tipo")]
    [ModalTextInput("addbet_tipo", TextInputStyle.Short, placeholder: "numero | vermelho | preto | par | impar | baixa | alta")]
    public string Tipo { get; set; } = string.Empty;

    [InputLabel("Alvo (número para tipo número)")]
    [ModalTextInput("addbet_alvo", TextInputStyle.Short, placeholder: "7")]
    public string Alvo { get; set; } = string.Empty;
}
