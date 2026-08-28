using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class CasinoTableBuilder
{
    public const string RoulCustomIdPrefix = "roul:";
    public const string RoulSpinAction = "spin";
    public const string RoulAddBetAction = "addbet";

    public const string SlotCustomIdPrefix = "slot:";
    public const string SlotSpinAction = "spin";

    private static readonly Dictionary<SlotSymbol, string> SlotEmoji = new()
    {
        [SlotSymbol.Cherry] = "🍒",
        [SlotSymbol.Lemon] = "🍋",
        [SlotSymbol.Bell] = "🔔",
        [SlotSymbol.Star] = "⭐",
        [SlotSymbol.Seven] = "7️⃣",
        [SlotSymbol.Diamond] = "💎"
    };

    public static Embed BuildRouletteTable(
        RouletteGame game, IUser player, int balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasSpun
            ? $"🎰 Resultado: **{FormatWinningNumber(game.ResultNumber!.Value)}**"
            : "🎰 Gire a roleta para revelar o resultado.");

        sb.AppendLine();

        foreach (var bet in game.Bets)
        {
            sb.AppendLine($"🎯 {FormatBet(bet)} — **{bet.Amount}** moedas");
        }

        if (game.Bets.Count == 0)
            sb.AppendLine("🎯 Nenhuma aposta feita ainda.");

        sb.AppendLine();
        sb.AppendLine($"💰 Aposta total: **{game.TotalBet}** moedas");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{balance}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Roleta")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Use os botões para apostar e girar, ou `macaco roleta` na próxima vez.");

        return embed.Build();
    }

    public static Embed BuildSlotTable(
        SlotMachineGame game, int bet, IUser player, int balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasSpun
            ? $"{FormatReels(game.Reels)}"
            : "🍒 | 🍋 | 🔔   ← Role a máquina!");

        sb.AppendLine();
        sb.AppendLine($"💰 Aposta: **{bet}** moedas");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{balance}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Caça-Níquel")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Use o botão para girar, ou `macaco cacaniquel` na próxima vez.");

        return embed.Build();
    }

    public static MessageComponent BuildRouletteComponents(bool hasBets)
    {
        return new ComponentBuilder()
            .WithButton("Adicionar aposta", $"{RoulCustomIdPrefix}{RoulAddBetAction}", ButtonStyle.Secondary, new Emoji("🎯"), disabled: hasBets)
            .WithButton("Girar", $"{RoulCustomIdPrefix}{RoulSpinAction}", ButtonStyle.Primary, new Emoji("🎰"), disabled: !hasBets)
            .Build();
    }

    public static MessageComponent BuildSlotComponents(bool hasSpun)
    {
        return new ComponentBuilder()
            .WithButton("Girar", $"{SlotCustomIdPrefix}{SlotSpinAction}", ButtonStyle.Primary, new Emoji("🎰"), disabled: hasSpun)
            .Build();
    }

    private static string FormatReels(IReadOnlyList<SlotSymbol> reels)
    {
        if (reels.Count < 3)
            return "? | ? | ?";
        return string.Join(" | ", reels.Select(s => SlotEmoji[s]));
    }

    private static string FormatWinningNumber(int number)
    {
        var color = RouletteGame.ColorOf(number);
        var colorEmoji = color switch
        {
            RouletteColor.Red => "🔴",
            RouletteColor.Black => "⚫",
            _ => "🟢"
        };
        var parity = number == 0 ? "🟢 Zero" : number % 2 == 0 ? "Par" : "Ímpar";
        return $"{colorEmoji} `{number}` ({color} · {parity})";
    }

    private static string FormatBet(RouletteBet bet)
    {
        return bet.Type switch
        {
            RouletteBetType.Number => $"Número `{bet.Target}`",
            RouletteBetType.Color => $"Cor {(RouletteColor)bet.Target}",
            RouletteBetType.Parity => bet.Target == 0 ? "Par" : "Ímpar",
            RouletteBetType.Half => bet.Target == 0 ? "Baixa (1–18)" : "Alta (19–36)",
            _ => "?"
        };
    }
}
