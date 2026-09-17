using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Utils;

public static class VideoPokerTableBuilder
{
    public const string CustomIdPrefix = "poker:";
    public const string HoldAction = "hold";
    public const string DrawAction = "draw";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildVideoPokerTable(
        VideoPokerState state, ulong bet, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine(FormatCards(state));
        if (state.HasDrawn)
        {
            sb.AppendLine($"📊 Mão: **{DescribeOutcome(state.Outcome ?? VideoPokerHandOutcome.None)}**");
        }
        else
        {
            sb.AppendLine("🎴 Escolha as cartas para segurar e troque o resto.");
        }

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
            .WithTitle("\U0001F3B0 Poker de Máquina")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Segure as cartas boas e troque o resto! 🃏");

        return embed.Build();
    }

    public static MessageComponent BuildHoldComponents(VideoPokerState state, ulong ownerId)
    {
        var builder = new ComponentBuilder();

        for (var i = 0; i < state.Hand.Count; i++)
        {
            var held = state.HeldPositions.Contains(i);
            builder.WithButton(
                new ButtonBuilder
                {
                    CustomId = $"{CustomIdPrefix}{HoldAction}:{i}",
                    Label = held ? $"🔒 {i + 1}" : $"{(i + 1)}",
                    Style = held ? ButtonStyle.Success : ButtonStyle.Secondary
                },
                row: 0);
        }

        builder
            .WithButton(
                new ButtonBuilder
                {
                    CustomId = $"{CustomIdPrefix}{DrawAction}",
                    Label = "Trocar",
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("🎴")
                },
                row: 1)
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"), row: 1)
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"), row: 1);

        return builder.Build();
    }

    public static MessageComponent BuildVideoPokerResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildVideoPokerPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Mãos e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nMão                         Pagamento");
        sb.AppendLine("────────────────────────────────");
        sb.AppendLine("Royal Flush                  250x");
        sb.AppendLine("Sequência de naipe            50x");
        sb.AppendLine("Quadra                        25x");
        sb.AppendLine("Full House                     9x");
        sb.AppendLine("Flush                          6x");
        sb.AppendLine("Sequência                      4x");
        sb.AppendLine("Trinca                         3x");
        sb.AppendLine("Dois Pares                     2x");
        sb.AppendLine("Par de Valetes+                1x");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• 5 cartas de um baralho de 52");
        sb.AppendLine("• Segure as cartas que quiser e troque as demais");
        sb.AppendLine("• Pagamento = aposta x multiplicador");

        return new EmbedBuilder()
            .WithTitle("🃏 Poker de Máquina — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribeOutcome(VideoPokerHandOutcome outcome) => outcome switch
    {
        VideoPokerHandOutcome.RoyalFlush => "Royal Flush",
        VideoPokerHandOutcome.StraightFlush => "Sequência de naipe",
        VideoPokerHandOutcome.FourOfAKind => "Quadra",
        VideoPokerHandOutcome.FullHouse => "Full House",
        VideoPokerHandOutcome.Flush => "Flush",
        VideoPokerHandOutcome.Straight => "Sequência",
        VideoPokerHandOutcome.ThreeOfAKind => "Trinca",
        VideoPokerHandOutcome.TwoPair => "Dois Pares",
        VideoPokerHandOutcome.JacksOrBetter => "Par de Valetes+",
        _ => "Nenhuma combinação"
    };

    private static string FormatCards(VideoPokerState state)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < state.Hand.Count; i++)
        {
            var held = state.HeldPositions.Contains(i);
            var prefix = held ? "🔒" : "";
            sb.Append($"`{prefix}{CasinoCards.CardText(state.Hand[i])}` ");
        }
        return sb.ToString().TrimEnd();
    }
}