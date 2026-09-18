using System.Text;
using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

[Group("banco", "Seu banco, poupança, extrato e ativos de renda")]
public partial class BancoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly ShopService _shop;

    public BancoSlashModule(IEconomyRepository economy, IEconomyAccessor accessor, ShopService shop)
    {
        _economy = economy;
        _accessor = accessor;
        _shop = shop;
    }

    [SlashCommand("ver", "Consulta seu banco, poupança e juros")]
    public async Task VerAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        var (bankMin, bankMax) = EconomyRules.GetBankInterestRange();
        var (savMin, savMax) = EconomyRules.GetInterestRateRange(profile.SavingsStreak);

        var bankEstMin = EconomyRules.ComputeInterestAmount(profile.Bank, bankMin);
        var bankEstMax = EconomyRules.ComputeInterestAmount(profile.Bank, bankMax);
        var savEstMin = EconomyRules.ComputeInterestAmount(profile.Savings, savMin);
        var savEstMax = EconomyRules.ComputeInterestAmount(profile.Savings, savMax);

        var positions = await _shop.GetOwnedAssetPositionsAsync(Context.User.Id);
        var totalIncomePerDay = positions
            .Sum(p => (long)EconomyRules.ComputeQuotaIncome(p.Asset.DailyIncome, p.Owned.Quantity));

        var sb = new StringBuilder();
        sb.AppendLine($"💰 **Carteira:** {EconomyFormat.Full(profile.Money)} moedas");
        sb.AppendLine($"🏦 **Banco:** {EconomyFormat.Full(profile.Bank)} moedas");
        sb.AppendLine($"   📈 CDB: **{bankMin:P2}–{bankMax:P2}**/dia → amanhã **+{EconomyFormat.Full(bankEstMin)}–{EconomyFormat.Full(bankEstMax)}**");
        sb.AppendLine($"💰 **Poupança:** {EconomyFormat.Full(profile.Savings)} moedas · streak **{profile.SavingsStreak}**");
        sb.AppendLine($"   📈 Juros: **{savMin:P2}–{savMax:P2}**/dia → amanhã **+{EconomyFormat.Full(savEstMin)}–{EconomyFormat.Full(savEstMax)}**");
        sb.AppendLine($"   🔁 Depositar 1x/dia mantém o streak; resgatar tudo zera.");
        if (positions.Count > 0)
            sb.AppendLine($"📈 **Ativos:** {positions.Count} posição(ões) · **+{EconomyFormat.Full((ulong)totalIncomePerDay)}/dia** → veja em `/banco ativos`");

        await RespondAsync(embed: new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Banco", Context.User.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use /banco depositar · sacar · poupar · resgatar · ativos")
            .Build());
    }

    [SlashCommand("depositar", "Move moedas da carteira para o banco")]
    public async Task DepositarAsync([Summary("valor", "Valor ou 'tudo'")] string valor)
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (!TryResolveAmount(valor, profile.Money, out var quantia, out var error))
        {
            await RespondAsync(error!, ephemeral: true);
            return;
        }

        var (success, wallet, bank) = await _economy.DepositAsync(mainId, quantia);

        if (!success)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira para depositar.", ephemeral: true);
            return;
        }

        await RespondAsync($"🏦 **{Context.User.GetDisplayName()}** depositou **{EconomyFormat.Full(quantia)} moedas** no banco!\nCarteira: **{EconomyFormat.Full(wallet)}** | Banco: **{EconomyFormat.Full(bank)}**");
    }

    [SlashCommand("sacar", "Move moedas do banco para a carteira")]
    public async Task SacarAsync([Summary("valor", "Valor ou 'tudo'")] string valor)
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (!TryResolveAmount(valor, profile.Bank, out var quantia, out var error))
        {
            await RespondAsync(error!, ephemeral: true);
            return;
        }

        var (success, wallet, bank) = await _economy.WithdrawAsync(mainId, quantia);

        if (!success)
        {
            await RespondAsync("❌ Você não tem moedas suficientes no banco para sacar.", ephemeral: true);
            return;
        }

        await RespondAsync($"🏧 **{Context.User.GetDisplayName()}** sacou **{EconomyFormat.Full(quantia)} moedas** do banco!\nCarteira: **{EconomyFormat.Full(wallet)}** | Banco: **{EconomyFormat.Full(bank)}**");
    }

    [SlashCommand("poupar", "Deposita na poupança com juros diários")]
    public async Task PouparAsync([Summary("valor", "Valor ou 'tudo'")] string valor)
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (!TryResolveAmount(valor, profile.Money, out var quantia, out var error))
        {
            await RespondAsync(error!, ephemeral: true);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.DepositSavingsAsync(mainId, quantia);

        if (!success)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na carteira para poupar.", ephemeral: true);
            return;
        }

        await RespondAsync(
            $"🏦 **{Context.User.GetDisplayName()}** depositou **{EconomyFormat.Full(quantia)} moedas** na poupança!\n" +
            $"Carteira: **{EconomyFormat.Full(wallet)}** | Poupança: **{EconomyFormat.Full(savings)}** | Streak: **{streak}**");
    }

    [SlashCommand("resgatar", "Saca moedas da poupança (parcial mantém o streak, resgatar tudo zera)")]
    public async Task ResgatarAsync([Summary("valor", "Valor ou 'tudo'")] string valor)
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (!TryResolveAmount(valor, profile.Savings, out var quantia, out var error))
        {
            await RespondAsync(error!, ephemeral: true);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.WithdrawSavingsAsync(mainId, quantia);

        if (!success)
        {
            await RespondAsync("❌ Você não tem moedas suficientes na poupança para resgatar.", ephemeral: true);
            return;
        }

        await RespondAsync(
            $"🏧 **{Context.User.GetDisplayName()}** resgatou **{EconomyFormat.Full(quantia)} moedas** da poupança!\n" +
            $"Carteira: **{EconomyFormat.Full(wallet)}** | Poupança: **{EconomyFormat.Full(savings)}**" +
            (savings == 0 ? " | Streak: **0**" : $" | Streak: **{streak}**"));
    }

    [SlashCommand("extrato", "Mostra o histórico de transações")]
    public async Task ExtratoAsync([Summary("quantidade", "Quantas transações mostrar (1-30)")] int quantidade = 10)
    {
        var txns = await _economy.GetHistoryAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), Math.Clamp(quantidade, 1, 30));

        if (txns.Count == 0)
        {
            await RespondAsync("🧾 Você ainda não tem transações registradas.");
            return;
        }

        var sb = new StringBuilder("🧾 **Histórico de transações**\n\n");
        foreach (var t in txns)
        {
            var sinal = t.Amount >= 0 ? "+" : "";
            sb.AppendLine($"`{t.CreatedAt:dd/MM HH:mm}` **{t.Type}** {sinal}{EconomyFormat.Full((ulong)Math.Abs(t.Amount))} — {t.Description}");
        }

        await RespondAsync(sb.ToString());
    }

    private static bool TryResolveAmount(string input, ulong balance, out ulong amount, out string? error)
    {
        if (input.Equals("tudo", StringComparison.OrdinalIgnoreCase)
            || input.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (balance == 0)
            {
                amount = 0;
                error = "⚠️ Você não tem moedas para isso.";
                return false;
            }

            amount = balance;
            error = null;
            return true;
        }

        return EconomyAmountParser.TryParse(input, out amount, out error);
    }
}