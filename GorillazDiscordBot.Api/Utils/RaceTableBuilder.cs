using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class RaceTableBuilder
{
    public const string CustomIdPrefix = "race:";
    public const string StartAction = "largada";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    private static readonly string[] RunnerEmojis =
    {
        "🐎", "🐴", "🦄", "🐎", "🐴", "🦄"
    };

    public static Embed BuildRaceTable(
        RaceGame game, ulong bet, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FormatLeaderboard(game));

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Corrida de Cavalo")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Escolha seu cavalo e torça na largada! 🐎");

        return embed.Build();
    }

    public static MessageComponent BuildRaceComponents(ulong ownerId)
    {
        return new ComponentBuilder()
            .WithButton("Largada", $"{CustomIdPrefix}{StartAction}", ButtonStyle.Primary, new Emoji("🏁"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static MessageComponent BuildRaceResultComponents(ulong ownerId, ulong bet, int pick)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{pick}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildRacePaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Como funciona**");
        sb.AppendLine();
        sb.AppendLine($"• **{CasinoRules.RaceRunners}** cavalos disputam a corrida");
        sb.AppendLine("• Escolha um cavalo e torça para ele vencer");
        sb.AppendLine("• Pagamento fixo de **5x** para o vencedor da aposta");

        return new EmbedBuilder()
            .WithTitle("🏇 Corrida de Cavalo — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribePick(int index) => $"Cavalo #{index + 1}";

    private static string FormatLeaderboard(RaceGame game)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < game.Runners; i++)
        {
            var pickBadge = i == game.PlayerPick ? "👉" : "　";
            var trophy = game.HasFinished && i == game.WinnerIndex ? " 🏆" : "";
            sb.AppendLine($"{pickBadge} {RunnerEmojis[i]} **{DescribePick(i)}**{trophy}");
        }
        return sb.ToString().TrimEnd();
    }
}