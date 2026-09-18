using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class EconomyModule : ModuleBase<SocketCommandContext>
{
    private readonly IEconomyRepository _economy;
    private readonly IEconomyAccessor _accessor;
    private readonly ShopService _shop;

    public EconomyModule(IEconomyRepository economy, IEconomyAccessor accessor, ShopService shop)
    {
        _economy = economy;
        _accessor = accessor;
        _shop = shop;
    }

    [Command("daily")]
    public async Task DailyAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        var baseReward = EconomyRules.GetDailyReward(Random.Shared);
        var dailyPetBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Daily);
        var petReward = baseReward * (ulong)dailyPetBonus / 100;
        var afterPet = baseReward + petReward;

        var boost = profile.DailyBoostPending;
        if (boost && profile.DailyBoostExpiresAt is { } exp && exp <= DateTime.UtcNow)
        {
            await _economy.SetDailyBoostAsync(mainId, false);
            boost = false;
        }

        var reward = boost ? afterPet * 2 : afterPet;

        var (claimed, newBalance) = await _economy.TryClaimDailyAsync(mainId, reward);

        if (!claimed)
        {
            await ReplyAsync("⏰ Você já resgatou seu daily hoje! Volte amanhã.");
            return;
        }

        if (boost)
            await _economy.SetDailyBoostAsync(mainId, false);

        var assetResult = await _shop.ApplyAssetIncomesAsync(mainId, Context.User.Username, boost);

        var sb = new StringBuilder("💰 **Daily resgatado!**\n");
        sb.AppendLine($"🎁 Valor base: **+{EconomyFormat.Full(baseReward)}** moedas");
        if (dailyPetBonus > 0)
            sb.AppendLine($"🐾 Bônus de pet (+{dailyPetBonus}%): **+{EconomyFormat.Full(petReward)}** moedas");
        if (boost)
            sb.AppendLine("⚡ **Com bônus x2!**");
        sb.AppendLine($"**Total do daily: +{EconomyFormat.Full(reward)} moedas**");

        if (assetResult.TotalIncome > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"📈 **Renda dos ativos** ({assetResult.DaysCollected} dia(s), {assetResult.ItemsCollected} ativo(s)):");
            foreach (var a in assetResult.Assets)
                sb.AppendLine($"   {a.Emoji} {a.Name}: **+{EconomyFormat.Full(a.Income)}** moedas ({a.Days} dia(s))");
            if (assetResult.PetBonusPercent > 0)
                sb.AppendLine($"   🐾 Bônus de pet de ativos (+{assetResult.PetBonusPercent}%): incluído");
            sb.AppendLine($"**Total ativos: +{EconomyFormat.Full(assetResult.TotalIncome)} moedas**");
        }

        var grandTotal = reward + assetResult.TotalIncome;
        var balance = newBalance + assetResult.TotalIncome;
        sb.AppendLine();
        sb.AppendLine($"💰 **Total recebido: +{EconomyFormat.Full(grandTotal)} moedas**");
        sb.AppendLine($"💰 Saldo atual: **{EconomyFormat.Full(balance)}** moedas");

        await ReplyAsync(sb.ToString());
    }

    [Command("saldo")]
    [Alias("carteira")]
    public async Task SaldoAsync()
    {
        await ReplyAsync("💳 A carteira agora é por slash command: use `/carteira ver`.\nPara transferências: `/carteira pagar <membro> <valor|tudo>`.");
    }

    [Command("pagar")]
    [Alias("pay")]
    public async Task PagarAsync(IUser receiver, [Remainder] string input)
    {
        await ReplyAsync("💳 Transferências agora são por slash command: use `/carteira pagar <membro> <valor|tudo>`.");
    }

    [Command("depositar")]
    [Alias("dep", "deposit")]
    public async Task DepositarAsync(string valor)
    {
        await ReplyAsync("🏦 Depósitos agora são por slash command: use `/banco depositar <valor|tudo>`.");
    }

    [Command("sacar")]
    [Alias("withdraw", "wd")]
    public async Task SacarAsync(string valor)
    {
        await ReplyAsync("🏧 Saques agora são por slash command: use `/banco sacar <valor|tudo>`.");
    }

    [Command("banco")]
    [Alias("savings", "bank", "banksaldo")]
    public async Task PoupancaAsync()
    {
        await ReplyAsync("🏦 O banco e a poupança agora são por slash command: use `/banco ver`.\nDeposite, saque, poupe e consulte sua renda fixa por lá.");
    }

    [Command("poupar")]
    [Alias("savingsdeposit")]
    public async Task PouparAsync(string valor)
    {
        await ReplyAsync("💰 Poupança agora é por slash command: use `/banco poupar <valor|tudo>`.");
    }

    [Command("resgatar")]
    [Alias("savingswithdraw")]
    public async Task ResgatarAsync(string valor)
    {
        await ReplyAsync("🏧 Resgates agora são por slash command: use `/banco resgatar <valor|tudo>`.");
    }

    [Command("trabalhar")]
    [Alias("work")]
    public async Task TrabalharAsync(string? servico = null)
    {
        await ReplyAsync(
            "💼 O trabalho agora é feito pelos slash commands!\n" +
            "• Use `/trabalho listar` para ver as profissões disponíveis.\n" +
            "• Use `/trabalho trabalhar <profissão>` para trabalhar.\n" +
            "• Use `/trabalho prova <profissão>` para tirar diplomas.");
    }

    [Command("roubar")]
    [Alias("rob")]
    public async Task RoubarAsync(IUser target)
    {
        if (target.IsBot)
        {
            await ReplyAsync("🤖 Não dá pra roubar um robô... por enquanto.");
            return;
        }

        if (target.Id == Context.User.Id)
        {
            await ReplyAsync("😂 Não dá pra roubar você mesmo.");
            return;
        }

        var attackerMain = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var victimMain = await _accessor.ResolveMainIdAsync(target.Id);

        if (attackerMain == victimMain)
        {
            await ReplyAsync("🔗 A conta de destino faz parte do seu próprio grupo vinculado.");
            return;
        }

        var now = DateTime.UtcNow;
        var attacker = await _economy.GetOrCreateAsync(attackerMain, Context.User.Username);

        if (attacker.RobCaughtUntil is { } caughtUntil && caughtUntil > now)
        {
            await ReplyAsync($"🚨 Você foi pego! Espere {FormatRemaining(caughtUntil - now)} para roubar de novo.");
            return;
        }

        if (EconomyRules.GetRemainingCooldown(attacker.LastRobTime, now, EconomyRules.RobCooldown) is { } rem)
        {
            await ReplyAsync($"⏳ Acalme-se... espere {FormatRemaining(rem)} para roubar.");
            return;
        }

        var victim = await _economy.GetOrCreateAsync(victimMain, target.Username);

        if (victim.RobShieldUntil is { } shieldUntil && shieldUntil > now)
        {
            await ReplyAsync($"🛡️ **{target.GetDisplayName()}** está protegido pelo Escudo Anti-Roubo por mais "
                + $"{FormatRemaining(shieldUntil - now)}. Nem tente!");
            return;
        }

        if (victim.Money < 1)
        {
            await ReplyAsync("😅 Essa pessoa está sem moedas na carteira.");
            return;
        }

        if (!await _economy.TryClaimRobAsync(attackerMain, now))
        {
            await ReplyAsync("🚨 Acalme-se... seu golpe ainda está em cooldown.");
            return;
        }

        if (EconomyRules.ShouldRobSucceed(Random.Shared))
        {
            ulong stolen = EconomyRules.ComputeRobAmount(victim.Money, Random.Shared);

            var robPetBonus = await _shop.GetUpgradePercentAsync(attackerMain, UpgradeEffect.Rob);
            if (robPetBonus > 0)
                stolen += stolen * (ulong)robPetBonus / 100;

            var (victimDeducted, _) = await _economy.TryDeductMoneyAsync(victimMain, stolen, EconomyTransactionType.Rob,
                $"Roubado por {Context.User.GetDisplayName()}");

            if (!victimDeducted)
            {
                await ReplyAsync("😅 Essa pessoa já não tem moedas suficientes na carteira.");
                return;
            }

            await _economy.AddMoneyAsync(attackerMain, stolen, EconomyTransactionType.Rob,
                $"Roubou {EconomyFormat.Full(stolen)} moedas de {target.GetDisplayName()}");

            await ReplyAsync($"🕵️ **Você roubou {EconomyFormat.Full(stolen)} moedas** de **{target.GetDisplayName()}**!");
        }
        else
        {
            await _economy.SetRobAttemptAsync(attackerMain, now, now.Add(EconomyRules.RobCaughtLockout));
            await ReplyAsync($"🚨 **Você foi pego roubando** **{target.GetDisplayName()}**! Ficará **3 horas** sem poder roubar.");
        }
    }

    [Command("historico")]
    [Alias("extrato")]
    public async Task HistoricoAsync(int limite = 10)
    {
        await ReplyAsync("🧾 O extrato agora é por slash command: use `/banco extrato <quantidade>`.");
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}min";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes}min";
        return $"{remaining.Seconds}seg";
    }

    public static class EconomyHelper
    {
        public static bool TryParsePositiveAmount(string input, out ulong amount, out string? error)
            => EconomyAmountParser.TryParse(input, out amount, out error);
    }
}