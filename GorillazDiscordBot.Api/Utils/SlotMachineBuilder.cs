using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class SlotMachineBuilder
{
    public static Embed BuildResult(SlotGame game, int winAmount, int balance)
    {
        var reels = string.Join(" │ ", game.Reels);

        var sb = new StringBuilder();
        sb.AppendLine($"🐵 {reels} 🐵");
        sb.AppendLine();

        if (winAmount > 0)
            sb.AppendLine($"🎉 **Você ganhou!** +{winAmount} moedas! (x{game.Multiplier})");
        else
            sb.AppendLine("😢 **Sem sorte!** Os macacos não trouxeram frutas. Você perdeu a aposta.");

        sb.AppendLine($"\U0001F4B0 Saldo atual: **{balance}** moedas");

        return new EmbedBuilder()
            .WithTitle("\U0001F3B0 Caça-Níqueis da Selva")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter("Puxe de novo: `/slots`")
            .Build();
    }
}
