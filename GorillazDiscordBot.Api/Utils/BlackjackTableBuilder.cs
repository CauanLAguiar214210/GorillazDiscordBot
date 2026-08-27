using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class BlackjackTableBuilder
{
    public const string HitAction = "hit";
    public const string StandAction = "stand";
    public const string DoubleAction = "double";

    public static bool CanDouble(BlackjackGame game) =>
        game.Phase == BlackjackPhase.PlayerTurn && game.Player.Cards.Count == 2 && !game.Doubled;

    public static Embed BuildTable(BlackjackGame game, IUser user) => BuildTable(game, user, null);

    public static Embed BuildTable(BlackjackGame game, IUser user, string? resultSection)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🃏 **Sua mão:** {HandText(game.Player)} — **{game.Player.Value}**");
        sb.AppendLine();
        sb.AppendLine($"🤖 **Mão do dealer:** {DealerText(game)}");
        sb.AppendLine();

        if (resultSection != null)
            sb.AppendLine(resultSection);
        else
            sb.AppendLine($"💰 Aposta: **{game.Bet}** moedas");

        var builder = new EmbedBuilder()
            .WithTitle("\u2660\uFE0F Blackjack da Selva")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use os botões para jogar");

        if (user != null)
            builder.WithAuthor(user);

        return builder.Build();
    }

    public static MessageComponent BuildActionComponents(BlackjackGame game)
    {
        var doubleButton = new ButtonBuilder()
            .WithLabel("💸 Dobrar")
            .WithCustomId($"bj:{DoubleAction}")
            .WithStyle(ButtonStyle.Success)
            .WithDisabled(!CanDouble(game));

        return new ComponentBuilder()
            .WithButton("🃏 Hit", $"bj:{HitAction}", ButtonStyle.Primary)
            .WithButton("✋ Stand", $"bj:{StandAction}", ButtonStyle.Secondary)
            .WithButton(doubleButton)
            .Build();
    }

    public static string DescribeResult(BlackjackGame game, int totalReturn)
    {
        var outcome = game.Outcome switch
        {
            BlackjackOutcome.PlayerBlackjack => "🎉 **Blackjack!** Você ganhou com 21 natural!",
            BlackjackOutcome.PlayerWin => "🎉 **Você ganhou!**",
            BlackjackOutcome.Push => "🤝 **Empate!** Sua aposta foi devolvida.",
            _ => "😢 **O dealer venceu.**"
        };

        return $"{outcome}\n💰 Retorno: **{totalReturn}** moedas (aposta {game.Bet}).";
    }

    private static string HandText(BlackjackHand hand) =>
        string.Join(" ", hand.Cards.Select(c => c.Symbol));

    private static string DealerText(BlackjackGame game)
    {
        if (game.DealerHoleHidden)
            return $"{game.Dealer.Cards.First().Symbol} 🂠";

        return $"{HandText(game.Dealer)} — **{game.Dealer.Value}**";
    }
}
