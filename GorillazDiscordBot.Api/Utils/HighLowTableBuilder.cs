using System.Globalization;
using System.Text;
using Discord;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Utils;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Utils;

public static class HighLowTableBuilder
{
    public const string CustomIdPrefix = "hl:";
    public const string HigherAction = "maior";
    public const string LowerAction = "menor";
    public const string CashOutAction = "sacar";
    public const string ReplayAction = "replay";
    public const string PaytableAction = "paytable";
    public const string LeaveAction = "leave";

    public static Embed BuildHighLowTable(
        HighLowState state, IUser player, ulong balance, string? resultSection = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🃏 Carta atual: **`{CasinoCards.CardText(state.CurrentCard)}`**");
        sb.AppendLine($"🎯 Multiplicador atual: **{FormatMultiplier(state.Multiplier)}**");

        if (resultSection != null)
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━");
            sb.Append(resultSection);
        }

        sb.AppendLine();
        sb.AppendLine($"💰 Saldo: **{EconomyFormat.Full(balance)}** moedas");

        var embed = new EmbedBuilder()
            .WithTitle("\U0001F3B0 Maior/Menor")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithAuthor($"{player.GetDisplayName()} no cassino", player.GetAvatarUrl());

        embed.WithStandardFooter("Acerte se a próxima carta é maior ou menor para multiplicar! 🃏");

        return embed.Build();
    }

    public static MessageComponent BuildHighLowComponents(HighLowState state, ulong ownerId)
    {
        return new ComponentBuilder()
            .WithButton("🔼 Maior", $"{CustomIdPrefix}{HigherAction}", ButtonStyle.Primary, disabled: !state.CanPickHigher)
            .WithButton("🔽 Menor", $"{CustomIdPrefix}{LowerAction}", ButtonStyle.Secondary, disabled: !state.CanPickLower)
            .WithButton("Sacar", $"{CustomIdPrefix}{CashOutAction}", ButtonStyle.Success, new Emoji("🪂"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static MessageComponent BuildHighLowResultComponents(ulong ownerId, ulong bet)
    {
        return new ComponentBuilder()
            .WithButton("Continuar", $"{CustomIdPrefix}{ReplayAction}:{ownerId}:{bet}", ButtonStyle.Success, new Emoji("🔄"))
            .WithButton("Pagamentos", $"{CustomIdPrefix}{PaytableAction}", ButtonStyle.Secondary, new Emoji("📊"))
            .WithButton("Sair", $"{CustomIdPrefix}{LeaveAction}:{ownerId}", ButtonStyle.Danger, new Emoji("🚪"))
            .Build();
    }

    public static Embed BuildHighLowPaytable()
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Regras**");
        sb.AppendLine();
        sb.AppendLine("• Decida se a **próxima carta** é maior 🔼 ou menor 🔽 que a atual");
        //sb.AppendLine("• Cada acerto soma **+2x** ao multiplicador");
        sb.AppendLine("• Carta **igual** é empate: nova carta, sem perder o multiplicador");
        sb.AppendLine("• Prever um lado impossível (Maior no Ás, Menor no 2) perde na hora");
        sb.AppendLine("• **Sacar** a qualquer momento para resgatar `aposta × multiplicador`");

        return new EmbedBuilder()
            .WithTitle("🃏 Maior/Menor — Tabela de Pagamentos")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .Build();
    }

    public static string FormatMultiplier(double value)
        => $"{value.ToString("0.00", CultureInfo.InvariantCulture)}x";

    public static string DescribeCard(LuckyMonkey.Contracts.State.CardDto card)
        => $"{CasinoCards.DescribeCard(card)}";
}