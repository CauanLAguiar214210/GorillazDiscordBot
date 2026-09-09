using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class MinesTableBuilder
{
    public const string CustomIdPrefix = "mines:";
    public const string RevealAction = "reveal";
    public const string CashOutAction = "sacar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildMinesTable(
        MinesGame game, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FormatGrid(game));
        sb.AppendLine();
        sb.AppendLine($"💣 Minas: **{game.MinesCount}** · 🟩 Reveladas: **{game.RevealedCount}**");
        sb.AppendLine($"🎯 Multiplicador atual: **{FormatMultiplier(game.CurrentMultiplier)}**");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Minas")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Escolha células seguras e saque antes de achar uma mina! 💥");

        return embed.Build();
    }

    public static MessageComponent BuildMinesComponents(MinesGame game, ulong ownerId)
    {
        var builder = new ComponentBuilder();

        for (var cell = 0; cell < game.GridSize; cell++)
        {
            builder.WithButton(
                new ButtonBuilder
                {
                    CustomId = $"{CustomIdPrefix}{RevealAction}:{cell}",
                    Emote = new Emoji("⬛"),
                    Style = ButtonStyle.Secondary,
                    IsDisabled = game.Revealed.Contains(cell)
                },
                row: cell / 4);
        }

        builder
            .WithButton("Sacar", $"{CustomIdPrefix}{CashOutAction}", ButtonStyle.Success, new Emoji("🪂"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"));

        return builder.Build();
    }

    public static MessageComponent BuildMinesResultComponents(ulong ownerId, ulong bet, int mines)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{mines}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildMinesPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Como funciona**");
        sb.AppendLine();
        sb.AppendLine("• Grade **4×4** com **{minas}** minas ocultas");
        sb.AppendLine("• Cada célula **segura** revelada aumenta o multiplicador");
        sb.AppendLine("• Ache uma **mina** e perde a aposta");
        sb.AppendLine("• **Sacar** resgata `aposta × multiplicador`");
        sb.AppendLine();
        sb.AppendLine("**Multiplicador justo** = ∏ (células restantes ÷ células seguras restantes)");
        sb.AppendLine("• A casa aplica uma margem de **5%** sobre o valor justo");

        return new EmbedBuilder()
            .WithTitle("🧨 Minas — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}x";

    private static string FormatGrid(MinesGame game)
    {
        var sb = new StringBuilder();
        for (var cell = 0; cell < game.GridSize; cell++)
        {
            if (game.Revealed.Contains(cell))
                sb.Append(game.Mines.Contains(cell) ? "💥" : "🟩");
            else if (game.HasBoom && game.Mines.Contains(cell))
                sb.Append("💣");
            else
                sb.Append("⬛");

            if (cell % 4 == 3)
                sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }
}