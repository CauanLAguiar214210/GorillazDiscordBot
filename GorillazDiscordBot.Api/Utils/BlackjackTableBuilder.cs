using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Utils;

public static class BlackjackTableBuilder
{
    private const string CardBackSymbol = "\U0001F0A0";

    public const string CustomIdPrefix = "bj:";
    public const string ResultCustomIdPrefix = "bjk:";

    public const string HitAction = "hit";
    public const string StandAction = "stand";
    public const string DoubleAction = "double";
    public const string PaytableAction = "paytable";
    public const string ReplayAction = "replay";
    public const string LeaveAction = "leave";

    public static Embed BuildTable(BlackjackState state, IUser player, string? resultSection = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\U0001FA99 Aposta: **{EconomyFormat.Full(state.Bet)}** moedas");
        sb.AppendLine();
        sb.AppendLine("🏦 **Dealer**");
        sb.AppendLine(FormatDealerHand(state));
        sb.AppendLine();
        sb.AppendLine($"🐒 **{player.GetDisplayName()}**");
        sb.AppendLine(FormatFullHand(state.PlayerCards, state.PlayerValue));

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.Append(resultSection);
        }

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F0CF Blackjack")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} na mesa", player.GetAvatarUrl());

        embed.WithStandardFooter("O objetivo é chegar o mais perto possível de 21 sem ultrapassá-lo, vencendo o dealer com uma pontuação maior.");

        return embed.Build();
    }

    public static MessageComponent BuildActionComponents(BlackjackState state)
    {
        return new ComponentBuilder()
            .WithButton("Pedir", $"{CustomIdPrefix}{HitAction}", ButtonStyle.Primary, new Emoji("🃏"))
            .WithButton("Parar", $"{CustomIdPrefix}{StandAction}", ButtonStyle.Success, new Emoji("✋"))
            .WithButton("Dobrar", $"{CustomIdPrefix}{DoubleAction}", ButtonStyle.Secondary, new Emoji("💰"), disabled: !state.CanDouble)
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .Build();
    }

    public static MessageComponent BuildResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{ResultCustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{ResultCustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{ResultCustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildBlackjackPaytable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Resultados e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nResultado            Pagamento");
        sb.AppendLine("──────────────────────────────");
        sb.AppendLine("Blackjack (inicial)      3:2 (2.5x)");
        sb.AppendLine("Vitória                   1:1 (2x)");
        sb.AppendLine("Empate (Push)          Devolve");
        sb.AppendLine("Derrota                  Perde");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• Dealer deve parar em 17 ou mais");
        sb.AppendLine("• Dealer pede em 16 ou menos");
        sb.AppendLine("• Dobrar só é permitido nas duas primeiras cartas");

        return new EmbedBuilder()
            .WithTitle("🃏 Blackjack — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    private static string FormatDealerHand(BlackjackState state)
    {
        return state.DealerHoleHidden && state.DealerCards.Count >= 2
            ? $"`{CasinoCards.CardText(state.DealerCards[0])}` `{CardBackSymbol}`"
            : FormatFullHand(state.DealerCards, state.DealerValue);
    }

    private static string FormatFullHand(IReadOnlyList<CardDto> cards, int value)
    {
        var cardText = CasinoCards.CardsText(cards);
        var valueText = value > 21 ? $"**{value}** 💥" : $"**{value}**";
        return $"{cardText} — {valueText}";
    }

    public static string DescribeResult(BlackjackState state, ulong totalReturn) => state.Outcome switch
    {
        BlackjackOutcome.PlayerBlackjack => $"\U0001F0CF **BLACKJACK!** Pagamento 3:2! Você recebeu **{EconomyFormat.Full(totalReturn)}** moedas.",
        BlackjackOutcome.PlayerWin when state.DealerValue > 21 => $"💥 O dealer estourou! **Você venceu!** Recebeu **{EconomyFormat.Full(totalReturn)}** moedas.",
        BlackjackOutcome.PlayerWin => $"🎉 **Você venceu!** Recebeu **{EconomyFormat.Full(totalReturn)}** moedas.",
        BlackjackOutcome.Push => "🤝 **Empate!** Sua aposta foi devolvida.",
        BlackjackOutcome.DealerWin when state.PlayerValue > 21 => $"💥 Você estourou! Perdeu **{EconomyFormat.Full(state.Bet)}** moedas.",
        _ => $"😢 **A casa venceu.** Perdeu **{EconomyFormat.Full(state.Bet)}** moedas."
    };
}