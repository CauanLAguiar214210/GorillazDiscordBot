using System.Globalization;
using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class WheelTableBuilder
{
    public const string CustomIdPrefix = "roda:";
    public const string SpinAction = "girar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildWheelTable(
        WheelGame game, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FormatWheel(game));

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Roda da Fortuna")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Gire a roda e veja onde ela para! 🎡");

        return embed.Build();
    }

    public static MessageComponent BuildWheelComponents()
    {
        return new ComponentBuilder()
            .WithButton("Girar", $"{CustomIdPrefix}{SpinAction}", ButtonStyle.Primary, new Emoji("🎡"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildWheelResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildWheelPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Fatias da roda (10)**");
        sb.AppendLine();
        sb.AppendLine("```\nMultiplicador   Probabilidade");
        sb.AppendLine("──────────────────────────────");
        sb.AppendLine("0.5x                    80%");
        sb.AppendLine("1.0x                    10%");
        sb.AppendLine("5.0x                    10%");
        sb.AppendLine("```");
        sb.AppendLine("**Regras:**");
        sb.AppendLine("• A casa aplica uma margem de **5%** sobre o pagamento da fatia");

        return new EmbedBuilder()
            .WithTitle("🎡 Roda da Fortuna — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", CultureInfo.InvariantCulture)}x";

    private static string FormatWheel(WheelGame game)
    {
        var sb = new StringBuilder();

        if (!game.HasSpun)
        {
            sb.AppendLine("🎡 A roda ainda não foi girada…");
            sb.AppendLine();
            sb.AppendLine("``0.5 0.5 0.5 0.5 0.5``");
            sb.AppendLine("`5.0          →          1.0`");
            sb.AppendLine("``0.5 0.5 0.5 0.5 0.5``");
            return sb.ToString().TrimEnd();
        }

        for (var i = 0; i < WheelGame.Slots.Length; i++)
        {
            var value = WheelGame.Slots[i];
            var marker = i == game.ResultIndex ? "**→**" : "　";
            var highlight = i == game.ResultIndex ? "**" : "";
            sb.AppendLine($"{marker} {highlight}{FormatMultiplier(value)}{highlight}");
        }

        return sb.ToString().TrimEnd();
    }
}