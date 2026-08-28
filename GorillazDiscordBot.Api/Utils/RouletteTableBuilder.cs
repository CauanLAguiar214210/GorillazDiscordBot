using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class RouletteTableBuilder
{
    public static Embed BuildResult(RouletteGame game, RouletteBet bet, int winAmount, int balance)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\U0001F300 Número: **{game.Result}** — {ColorName(game.Color)}");
        sb.AppendLine();
        sb.AppendLine($"\U0001F4B0 Aposta: **{bet.Describe()}**");
        sb.AppendLine();

        if (winAmount > 0)
            sb.AppendLine($"🎉 **Você ganhou!** +{winAmount} moedas!");
        else
            sb.AppendLine("😢 **A casa venceu.** Você perdeu a aposta.");

        sb.AppendLine($"\U0001F4B0 Saldo atual: **{balance}** moedas");

        return new EmbedBuilder()
            .WithTitle("\U0001F300 Roleta da Selva")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter("Outra rodada: `/roleta`")
            .Build();
    }

    private static string ColorName(RouletteColor color) => color switch
    {
        RouletteColor.Red => "🔴 Vermelho",
        RouletteColor.Black => "⚫ Preto",
        _ => "🟢 Zero"
    };
}
