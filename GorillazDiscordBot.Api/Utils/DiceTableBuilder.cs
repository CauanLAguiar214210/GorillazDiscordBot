using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class DiceTableBuilder
{
    public const string CustomIdPrefix = "dic:";
    public const string RollAction = "roll";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    private static readonly string[] DiceFaces = { "⚀", "⚁", "⚂", "⚃", "⚄", "⚅" };

    public static Embed BuildDiceTable(
        DiceGame game, DiceBetType betType, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasRolled
            ? $"🎲 **{DescribeDice(game)}** — total **{game.Total}**"
            : "🎲 | ? | ?   ← Role os dados!");

        sb.AppendLine();
        sb.AppendLine($"🎯 Aposta: **{DescribeBetType(betType)}**");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Dados")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Use o botão para rolar, ou `macaco dado` na próxima vez.");

        return embed.Build();
    }

    public static MessageComponent BuildDiceComponents()
    {
        return new ComponentBuilder()
            .WithButton("Rolar", $"{CustomIdPrefix}{RollAction}", ButtonStyle.Primary, new Emoji("🎲"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildDiceResultComponents(ulong ownerId, ulong bet, DiceBetType type)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{(int)type}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildDicePaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Tipos de Aposta e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nAposta                Condição       Pagamento");
        sb.AppendLine("─────────────────────────────────────────────");
        sb.AppendLine("Baixa (2–6)         soma ≤ 6         2x");
        sb.AppendLine("Alta (8–12)         soma ≥ 8         2x");
        sb.AppendLine("Sete (7)            soma = 7         5x");
        sb.AppendLine("Dupla               dados iguais     6x");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• Dois dados de 6 lados são rolados");
        sb.AppendLine("• Pagamento = aposta x multiplicador");

        return new EmbedBuilder()
            .WithTitle("🎲 Dados — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribeBetType(DiceBetType type) => type switch
    {
        DiceBetType.High => "Alta (8–12)",
        DiceBetType.Low => "Baixa (2–6)",
        DiceBetType.Seven => "Sete (7)",
        DiceBetType.Doubles => "Dupla (pares)",
        _ => "?"
    };

    private static string DescribeDice(DiceGame game)
    {
        var (first, second) = game.Result!.Value;
        var pairBadge = game.IsDoubles && first != 7 ? "  🎯 Dupla!" : "";
        return $"{DiceFaces[first - 1]} {DiceFaces[second - 1]}{pairBadge}";
    }
}