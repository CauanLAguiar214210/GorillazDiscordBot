using System.Globalization;
using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class AviaoTableBuilder
{
    public const string CustomIdPrefix = "aviao:";
    public const string FlyAction = "fly";
    public const string CashOutAction = "peg";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildAviaoTable(
        AviaoGame game, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(DescribeFlight(game));

        sb.AppendLine();
        sb.AppendLine($"💰 Aposta: **{EconomyFormat.Full(game.Bet)}** moedas");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Aviaozinho")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter(resultSection == null
            ? "Pule antes que o aviãozinho exploda! ✈️"
            : "Novo voo: `/aviaozinho`");

        return embed.Build();
    }

    public static MessageComponent BuildFlightComponents()
    {
        return new ComponentBuilder()
            .WithButton("Voar", $"{CustomIdPrefix}{FlyAction}", ButtonStyle.Primary, new Emoji("✈️"))
            .WithButton("Pegar", $"{CustomIdPrefix}{CashOutAction}", ButtonStyle.Success, new Emoji("🪂"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildAviaoResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildAviaoPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Como funciona**");
        sb.AppendLine();
        sb.AppendLine("```\nMultiplicador       Pagamento");
        sb.AppendLine("──────────────────────────────");
        sb.AppendLine("1.10x                1.1x (110%)");
        sb.AppendLine("1.50x                1.5x (150%)");
        sb.AppendLine("2.00x                2x (200%)");
        sb.AppendLine("5.00x                5x (500%)");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• O multiplicador sobe **+0.10x** a cada clique em Voar");
        sb.AppendLine("• O aviãozinho explode em um ponto aleatório (1.00x a 5.00x)");
        sb.AppendLine("• Aperte **Pegar** antes de explodir para resgatar");
        sb.AppendLine("• Pagamento = aposta x multiplicador atual");

        return new EmbedBuilder()
            .WithTitle("✈️ Aviaozinho — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    private static string DescribeFlight(AviaoGame game)
    {
        if (game.HasCrashed)
            return $"💥 **Crash!** O aviãozinho explodiu em **{FormatMultiplier(game.CrashMultiplier)}**.";

        if (game.HasCashedOut)
            return $"🪂 Você pulou em **{FormatMultiplier(game.CurrentMultiplier)}**!";

        return $"✈️ Voando… multiplicador atual **{FormatMultiplier(game.CurrentMultiplier)}**";
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", CultureInfo.InvariantCulture)}x";
}