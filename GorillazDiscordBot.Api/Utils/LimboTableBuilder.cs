using System.Globalization;
using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class LimboTableBuilder
{
    public const string CustomIdPrefix = "limbo:";
    public const string RevealAction = "revelar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildLimboTable(
        LimboGame game, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(game.HasRolled
            ? DescribeRoll(game)
            : "🔮 O número ainda não foi revelado…");

        sb.AppendLine();
        sb.AppendLine($"🎯 Alvo: **{FormatMultiplier(game.Target)}**");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Limbo")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Se o número passar do alvo, você ganha! 🔮");

        return embed.Build();
    }

    public static MessageComponent BuildLimboComponents()
    {
        return new ComponentBuilder()
            .WithButton("Revelar", $"{CustomIdPrefix}{RevealAction}", ButtonStyle.Primary, new Emoji("🔮"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildLimboResultComponents(ulong ownerId, ulong bet, int targetCents)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{targetCents}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildLimboPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Como funciona**");
        sb.AppendLine();
        sb.AppendLine("• Você escolhe um **alvo** multiplicador");
        sb.AppendLine("• O jogo sorteia um número aleatório (1.00x a 10.000x)");
        sb.AppendLine("• Se o número for **≥ alvo**, você ganha `aposta × alvo`");
        sb.AppendLine("• A casa aplica uma margem de **3%** sobre o pagamento");
        sb.AppendLine();
        sb.AppendLine("**Alvos disponíveis:** 2x, 3x, 5x, 10x");

        return new EmbedBuilder()
            .WithTitle("🔮 Limbo — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", CultureInfo.InvariantCulture)}x";

    private static string DescribeRoll(LimboGame game)
    {
        var badge = game.IsWin ? " ✅✨" : " ❌";
        return $"🔮 Resultado: **{FormatMultiplier(game.Result!.Value)}**{badge}";
    }
}