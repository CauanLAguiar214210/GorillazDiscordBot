using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class PlinkoTableBuilder
{
    public const string CustomIdPrefix = "plinko:";
    public const string DropAction = "largar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildPlinkoTable(
        PlinkoGame game, ulong bet, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FormatBoard(game));
        sb.AppendLine(game.HasDropped
            ? $"🎱 A bolinha caiu na faixa **{game.ResultBin!.Value + 1}** — {FormatMultiplier(PlinkoGame.Multiplier(game.ResultBin.Value))}"
            : "🎱 A bolinha está no topo…");

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
            .WithTitle("\U0001F3B0 Plinko")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Solte a bolinha e veja onde ela cai! 📌");

        return embed.Build();
    }

    public static MessageComponent BuildPlinkoComponents()
    {
        return new ComponentBuilder()
            .WithButton("Largar", $"{CustomIdPrefix}{DropAction}", ButtonStyle.Primary, new Emoji("🎱"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildPlinkoResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildPlinkoPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Faixas e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nFaixa   Multiplicador   Probabilidade");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine("1, 9        3.0x           ~0.8%");
        sb.AppendLine("2, 8        2.0x           ~6%");
        sb.AppendLine("3, 7        1.4x          ~22%");
        sb.AppendLine("4, 6        0.9x          ~44%");
        sb.AppendLine("5          0.5x          ~27%");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• A bolinha desce por pinos e cai em uma das 9 faixas");
        sb.AppendLine("• Pagamento = aposta x multiplicador da faixa");

        return new EmbedBuilder()
            .WithTitle("📌 Plinko — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}x";

    private static string FormatBoard(PlinkoGame game)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```");
        sb.AppendLine("  2.0  1.4  0.9  0.5  0.9  1.4  2.0");
        sb.AppendLine("  3.0            🎱            3.0");
        sb.AppendLine("```");
        return sb.ToString().TrimEnd();
    }
}