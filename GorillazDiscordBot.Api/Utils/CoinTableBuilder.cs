using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class CoinTableBuilder
{
    public const string CustomIdPrefix = "coin:";
    public const string FlipAction = "flip";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildCoinFlipTable(
        CoinFlipGame game, CoinSide chosen, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasFlipped
            ? $"🪙 Caiu: **{DescribeSide(game.Result!.Value)}** {DescribeEmoji(game.Result!.Value)}"
            : "🪙 A moeda está no ar…");

        sb.AppendLine();
        sb.AppendLine($"🎯 Aposta: **{DescribeSide(chosen)}**");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Cara ou Coroa")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Use o botão para lançar, ou `macaco flip` na próxima vez.");

        return embed.Build();
    }

    public static MessageComponent BuildCoinComponents()
    {
        return new ComponentBuilder()
            .WithButton("Lançar", $"{CustomIdPrefix}{FlipAction}", ButtonStyle.Primary, new Emoji("🪙"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildCoinResultComponents(ulong ownerId, ulong bet, CoinSide side)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{(int)side}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildCoinPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Resultados e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nResultado            Pagamento");
        sb.AppendLine("──────────────────────────────");
        sb.AppendLine("Cara                     2x");
        sb.AppendLine("Coroa                    2x");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• Chance de 50/50 para cada lado");
        sb.AppendLine("• Pagamento = aposta x 2");

        return new EmbedBuilder()
            .WithTitle("🪙 Cara ou Coroa — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribeSide(CoinSide side) => side switch
    {
        CoinSide.Cara => "Cara",
        CoinSide.Coroa => "Coroa",
        _ => "?"
    };

    private static string DescribeEmoji(CoinSide side) => side switch
    {
        CoinSide.Cara => "🦅",
        CoinSide.Coroa => "🌙",
        _ => "❓"
    };
}