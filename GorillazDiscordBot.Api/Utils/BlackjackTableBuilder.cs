using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class BlackjackTableBuilder
{
    private const string CardBackSymbol = "\U0001F0A0";

    public const string CustomIdPrefix = "bj:";

    public const string HitAction = "hit";
    public const string StandAction = "stand";
    public const string DoubleAction = "double";

    public static bool CanDouble(BlackjackGame game)
        => game.Player.Cards.Count == 2 && !game.Doubled;

    public static Embed BuildTable(BlackjackGame game, IUser player, string? resultSection = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\U0001FA99 Aposta: **{game.Bet}** moedas");
        sb.AppendLine();
        sb.AppendLine("🏦 **Dealer**");
        sb.AppendLine(FormatDealerHand(game));
        sb.AppendLine();
        sb.AppendLine($"🐒 **{player.GetDisplayName()}**");
        sb.AppendLine(FormatFullHand(game.Player));

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

        embed.WithStandardFooter(resultSection == null
            ? "Use os botões abaixo ou `macaco hit` / `macaco stand` / `macaco double`"
            : "Nova mão: `/blackjack` ou `macaco blackjack <valor>`");

        return embed.Build();
    }

    public static MessageComponent BuildActionComponents(BlackjackGame game)
    {
        return new ComponentBuilder()
            .WithButton("Pedir", $"{CustomIdPrefix}{HitAction}", ButtonStyle.Primary, new Emoji("🃏"))
            .WithButton("Parar", $"{CustomIdPrefix}{StandAction}", ButtonStyle.Success, new Emoji("✋"))
            .WithButton("Dobrar", $"{CustomIdPrefix}{DoubleAction}", ButtonStyle.Secondary, new Emoji("💰"), disabled: !CanDouble(game))
            .Build();
    }

    private static string FormatDealerHand(BlackjackGame game)
    {
        return game.DealerHoleHidden
            ? $"`{game.Dealer.Cards[0].Symbol}` `{CardBackSymbol}`"
            : FormatFullHand(game.Dealer);
    }

    private static string FormatFullHand(BlackjackHand hand)
    {
        var cards = string.Join(" ", hand.Cards.Select(c => $"`{c.Symbol}`"));
        var value = hand.IsBust ? $"**{hand.Value}** 💥" : $"**{hand.Value}**";
        return $"{cards} — {value}";
    }

    public static string DescribeResult(BlackjackGame game, int totalReturn) => game.Outcome switch
    {
        BlackjackOutcome.PlayerBlackjack => $"\U0001F0CF **BLACKJACK!** Pagamento 3:2! Você recebeu **{totalReturn}** moedas.",
        BlackjackOutcome.PlayerWin when game.Dealer.IsBust => $"💥 O dealer estourou! **Você venceu!** Recebeu **{totalReturn}** moedas.",
        BlackjackOutcome.PlayerWin => $"🎉 **Você venceu!** Recebeu **{totalReturn}** moedas.",
        BlackjackOutcome.Push => "🤝 **Empate!** Sua aposta foi devolvida.",
        BlackjackOutcome.DealerWin when game.Player.IsBust => $"💥 Você estourou! Perdeu **{game.Bet}** moedas.",
        _ => $"😢 **A casa venceu.** Perdeu **{game.Bet}** moedas."
    };
}
