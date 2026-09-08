using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class CasinoTableBuilder
{
    public const string RoulCustomIdPrefix = "roul:";
    public const string RoulSpinAction = "spin";
    public const string RoulAddBetAction = "addbet";
    public const string RoulPaytableAction = "paytable";
    public const string RoulReplayAction = "replay";
    public const string RoulLeaveAction = "leave";

    public const string SlotCustomIdPrefix = "slot:";
    public const string SlotSpinAction = "spin";
    public const string SlotPaytableAction = "paytable";
    public const string SlotReplayAction = "replay";
    public const string SlotLeaveAction = "leave";

    private static readonly Dictionary<SlotSymbol, string> SlotEmoji = new()
    {
        [SlotSymbol.Clover] = "🍀",
        [SlotSymbol.Cherry] = "🍒",
        [SlotSymbol.Lemon] = "🍋",
        [SlotSymbol.Bell] = "🔔",
        [SlotSymbol.Star] = "⭐",
        [SlotSymbol.Skull] = "💀",
        [SlotSymbol.Seven] = "7️⃣",
        [SlotSymbol.Diamond] = "💎",
        [SlotSymbol.Crown] = "👑"
    };

    public static string? DescribeAppliedRelic(PayoutResult payout)
    {
        if (payout.Bonus == 0 || payout.Relic is not { } relic)
            return null;

        return relic.Effect == RelicEffect.Cashback
            ? $"{relic.Emoji} **{relic.Name}** (devolve {relic.Value}% da aposta): +**{EconomyFormat.Full(payout.Bonus)}** moedas devolvidas"
            : $"{relic.Emoji} **{relic.Name}** (+{relic.Value}%): +**{EconomyFormat.Full(payout.Bonus)}** moedas extras";
    }

    public static Embed BuildRouletteTable(
        RouletteGame game, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasSpun
            ? $"🎰 Resultado: **{FormatWinningNumber(game.ResultNumber!.Value)}**"
            : "🎰 Gire a roleta para revelar o resultado.");

        sb.AppendLine();

        foreach (var bet in game.Bets)
        {
            sb.AppendLine($"🎯 {FormatBet(bet)} — **{EconomyFormat.Full(bet.Amount)}** moedas");
        }

        if (game.Bets.Count == 0)
            sb.AppendLine("🎯 Nenhuma aposta feita ainda.");

        sb.AppendLine();
        sb.AppendLine($"💰 Aposta total: **{EconomyFormat.Full(game.TotalBet)}** moedas");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Roleta")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Use os botões para apostar e girar, ou `macaco roleta` na próxima vez.");

        return embed.Build();
    }

    public static Embed BuildSlotTable(
        SlotMachineGame game, ulong bet, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasSpun
            ? $"{FormatReels(game.Reels)}"
            : "🍒 | 🍋 | 🔔   ← Role a máquina!");

        sb.AppendLine();
        sb.AppendLine($"💰 Aposta: **{EconomyFormat.Full(bet)}** moedas");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

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
            .WithButton("Pagamentos", $"{RoulCustomIdPrefix}{RoulPaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildSlotComponents(bool hasSpun)
    {
        return new ComponentBuilder()
            .WithButton("Girar", $"{SlotCustomIdPrefix}{SlotSpinAction}", ButtonStyle.Primary, new Emoji("🎰"), disabled: hasSpun)
            .WithButton("Pagamentos", $"{SlotCustomIdPrefix}{SlotPaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildSlotReplayComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Girar", $"{SlotCustomIdPrefix}{SlotReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🎰"))
            .WithButton("Pagamentos", $"{SlotCustomIdPrefix}{SlotPaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{SlotCustomIdPrefix}{SlotLeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static MessageComponent BuildRouletteReplayComponents(ulong ownerId, ulong bet, RouletteBetType type, int target)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{RoulCustomIdPrefix}{RoulReplayAction}:{ownerId}:{bet}:{(int)type}:{target}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{RoulCustomIdPrefix}{RoulPaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{RoulCustomIdPrefix}{RoulLeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildSlotPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Símbolos e Multiplicadores**");
        sb.AppendLine();
        sb.AppendLine("```\nSímbolo     Tripla    Dupla");
        sb.AppendLine("─────────────────────────────");
        sb.AppendLine("🍀 Clover     2x        1x");
        sb.AppendLine("🍒 Cherry     2x        1x");
        sb.AppendLine("🍋 Lemon      3x        2x");
        sb.AppendLine("🔔 Bell       4x        3x");
        sb.AppendLine("⭐ Star       6x        3x");
        sb.AppendLine("💀 Skull     10x        4x");
        sb.AppendLine("7️⃣ Seven     15x        7x");
        sb.AppendLine("💎 Diamond   40x       12x");
        sb.AppendLine("👑 Crown     50x       15x");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• **Tripla** = três símbolos iguais");
        sb.AppendLine("• **Dupla** = dois símbolos iguais");
        sb.AppendLine("• Pagamento = aposta x multiplicador");
        sb.AppendLine("• Sem combinação = perde a aposta");

        return new EmbedBuilder()
            .WithTitle("🎰 Caça-Níquel — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static Embed BuildRoulettePaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Tipos de Aposta e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nAposta                Pagamento");
        sb.AppendLine("──────────────────────────────────");
        sb.AppendLine("Número (0–36)          36x (35:1)");
        sb.AppendLine("Cor (Vermelho/Preto)    2x (1:1)");
        sb.AppendLine("Par/Ímpar                2x (1:1)");
        sb.AppendLine("Baixa (1–18)             2x (1:1)");
        sb.AppendLine("Alta (19–36)             2x (1:1)");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• O número **0** não conta como cor, par/ímpar ou metade (pagamento perde)");
        sb.AppendLine("• Pagamento = aposta x multiplicador");

        return new EmbedBuilder()
            .WithTitle("🎡 Roleta — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
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