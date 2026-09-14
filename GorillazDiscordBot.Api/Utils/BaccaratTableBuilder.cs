using System.Globalization;
using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Utils;

public static class BaccaratTableBuilder
{
    public const string CustomIdPrefix = "bac:";
    public const string RevealAction = "revelar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildBaccaratTable(
        BaccaratState state, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🎴 Aposta: **{DescribeBet(state.BetType)}**");

        sb.AppendLine();
        sb.AppendLine($"✋ **Jogador** — {DescribeValue(state.PlayerValue)}");
        sb.AppendLine(CasinoCards.CardsText(state.Player));
        if (!state.HasRevealed && state.Player.Count >= 3)
            sb.AppendLine("`🂠`");

        sb.AppendLine();
        sb.AppendLine($"🏦 **Banco** — {DescribeValue(state.BankerValue)}");
        sb.AppendLine(CasinoCards.CardsText(state.Banker));
        if (!state.HasRevealed && state.Banker.Count >= 3)
            sb.AppendLine("`🂠`");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Baccarat")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Aplique a regra da terceira carta e veja quem vence! 🎴");

        return embed.Build();
    }

    public static MessageComponent BuildBaccaratComponents(ulong ownerId)
    {
        return new ComponentBuilder()
            .WithButton("Revelar", $"{CustomIdPrefix}{RevealAction}", ButtonStyle.Primary, new Emoji("🎴"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static MessageComponent BuildBaccaratResultComponents(ulong ownerId, ulong bet, int betType)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}:{betType}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildBaccaratPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Apostas e Pagamentos**");
        sb.AppendLine();
        sb.AppendLine("```\nAposta      Pagamento");
        sb.AppendLine("──────────────────────");
        sb.AppendLine("Jogador     1:1 (2x)");
        sb.AppendLine("Banco       0.95:1 (1.95x)");
        sb.AppendLine("Empate      8:1 (9x)");
        sb.AppendLine("```");

        sb.AppendLine("**Regras:**");
        sb.AppendLine("• Cada lado recebe **2 cartas** (valor = soma % 10; Ás = 1, 10–K = 0)");
        sb.AppendLine("• Natural **8** ou **9** encerra na hora");
        sb.AppendLine("• Jogador puxa com **≤ 5**; Banco segue a tabela oficial da 3ª carta");

        return new EmbedBuilder()
            .WithTitle("🎴 Baccarat — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string DescribeBet(BaccaratBetType type) => type switch
    {
        BaccaratBetType.Player => "**Jogador** ✋ (2x)",
        BaccaratBetType.Banker => "**Banco** 🏦 (1.95x)",
        BaccaratBetType.Tie => "**Empate** 🤝 (9x)",
        _ => "?"
    };

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", CultureInfo.InvariantCulture)}x";

    private static string DescribeValue(int value) => value == 0 ? "(zero)" : $"({value})";
}