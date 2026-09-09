using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Games.Casino;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class RpsTableBuilder
{
    public const string CustomIdPrefix = "rps:";
    public const string PlayAction = "jogar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildRpsTable(
        RpsGame game, ulong bet, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🤚 Sua jogada: **{DescribeMove(game.PlayerMove)}** {Emoji(game.PlayerMove)}");
        sb.AppendLine(game.HasPlayed
            ? $"🤖 Oponente: **{DescribeMove(game.OpponentMove!.Value)}** {Emoji(game.OpponentMove.Value)}"
            : "🤖 Oponente: aguardando…");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Jokenpô")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Vitória paga 2x, empate devolve a aposta! ✌️");

        return embed.Build();
    }

    public static MessageComponent BuildRpsComponents()
    {
        return new ComponentBuilder()
            .WithButton("Jogar", $"{CustomIdPrefix}{PlayAction}", ButtonStyle.Primary, new Emoji("🤚"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildRpsResultComponents(ulong ownerId, ulong bet, int move)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{move}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildRpsPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Regras**");
        sb.AppendLine();
        sb.AppendLine("```\nResultado            Pagamento");
        sb.AppendLine("──────────────────────────────");
        sb.AppendLine("Vitória                  2x");
        sb.AppendLine("Empate               Devolve");
        sb.AppendLine("Derrota                 Perde");
        sb.AppendLine("```");
        sb.AppendLine("**Combinações:** Pedra ✊ vence Tesoura ✌️ · Tesoura vence Papel ✋ · Papel vence Pedra");

        return new EmbedBuilder()
            .WithTitle("🤚 Jokenpô — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribeMove(RpsMove move) => move switch
    {
        RpsMove.Pedra => "Pedra",
        RpsMove.Papel => "Papel",
        RpsMove.Tesoura => "Tesoura",
        _ => "?"
    };

    private static string Emoji(RpsMove move) => move switch
    {
        RpsMove.Pedra => "✊",
        RpsMove.Papel => "✋",
        RpsMove.Tesoura => "✌️",
        _ => "❓"
    };
}