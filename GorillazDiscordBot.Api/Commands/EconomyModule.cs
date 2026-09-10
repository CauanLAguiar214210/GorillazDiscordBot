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
        var user = await _economy.GetOrCreateAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), Context.User.Username);
        await ReplyAsync($"💰 **{Context.User.GetDisplayName()}**, seu saldo é **{EconomyFormat.Full(user.Money)} moedas** na carteira.");
    }

    [Command("pagar")]
    [Alias("pay")]
    public async Task PagarAsync(IUser receiver, [Remainder] string input)
    {
        if (!EconomyHelper.TryParsePositiveAmount(input, out ulong quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        if (receiver.IsBot)
        {
            await ReplyAsync("🤖 Não posso aceitar moedas, mas obrigado!");
            return;
        }

        if (receiver.Id == Context.User.Id)
        {
            await ReplyAsync("😂 Não dá pra pagar você mesmo.");
            return;
        }

        var senderMain = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var receiverMain = await _accessor.ResolveMainIdAsync(receiver.Id);

        if (senderMain == receiverMain)
        {
            await ReplyAsync("🔗 A conta de destino faz parte do seu próprio grupo vinculado.");
            return;
        }

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            senderMain, quantia, EconomyTransactionType.Payment,
            $"Pagamento para {receiver.GetDisplayName()}");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        await _economy.GetOrCreateAsync(receiverMain, receiver.Username);
        await _economy.AddMoneyAsync(receiverMain, quantia, EconomyTransactionType.Payment,
            $"Pagamento de {Context.User.GetDisplayName()}");

        await ReplyAsync($"💸 **{Context.User.GetDisplayName()}** pagou **{EconomyFormat.Full(quantia)} moedas** para **{receiver.GetDisplayName()}**!");
    }

    [Command("depositar")]
    [Alias("dep", "deposit")]
    public async Task DepositarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out ulong quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, bank) = await _economy.DepositAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para depositar.");
            return;
        }

        await ReplyAsync($"🏦 **{Context.User.GetDisplayName()}** depositou **{EconomyFormat.Full(quantia)} moedas** no banco!\nCarteira: **{EconomyFormat.Full(wallet)}** | Banco: **{EconomyFormat.Full(bank)}**");
    }

    [Command("sacar")]
    [Alias("withdraw", "wd")]
    public async Task SacarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out ulong quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, bank) = await _economy.WithdrawAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes no banco para sacar.");
            return;
        }

        await ReplyAsync($"🏧 **{Context.User.GetDisplayName()}** sacou **{EconomyFormat.Full(quantia)} moedas** do banco!\nCarteira: **{EconomyFormat.Full(wallet)}** | Banco: **{EconomyFormat.Full(bank)}**");
    }

    [Command("banco")]
    [Alias("savings", "bank", "banksaldo")]
    public async Task PoupancaAsync()
    {
        var user = await _economy.GetOrCreateAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), Context.User.Username);

        var min = EconomyRules.DailyInterestMin + Math.Min(user.SavingsStreak, EconomyRules.InterestStreakMaxBonus) * EconomyRules.InterestStreakBonus;
        var max = EconomyRules.DailyInterestMax + Math.Min(user.SavingsStreak, EconomyRules.InterestStreakMaxBonus) * EconomyRules.InterestStreakBonus;

        await ReplyAsync(
            $"🏦 **{Context.User.GetDisplayName()}**\n" +
            $"Carteira: **{EconomyFormat.Full(user.Money)}**\nBanco: **{EconomyFormat.Full(user.Bank)}**\n" +
            $"Poupança: **{EconomyFormat.Full(user.Savings)}** (streak: **{user.SavingsStreak}**)\n" +
            $"Juros diários: **{min:P1}–{max:P1}**");
    }

    [Command("poupar")]
    [Alias("savingsdeposit")]
    public async Task PouparAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out ulong quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.DepositSavingsAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para poupar.");
            return;
        }

        await ReplyAsync(
            $"🏦 **{Context.User.GetDisplayName()}** depositou **{EconomyFormat.Full(quantia)} moedas** na poupança!\n" +
            $"Carteira: **{EconomyFormat.Full(wallet)}** | Poupança: **{EconomyFormat.Full(savings)}** | Streak: **{streak}**");
    }

    [Command("resgatar")]
    [Alias("savingswithdraw")]
    public async Task ResgatarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out ulong quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.WithdrawSavingsAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na poupança para resgatar.");
            return;
        }

        await ReplyAsync(
            $"🏧 **{Context.User.GetDisplayName()}** resgatou **{EconomyFormat.Full(quantia)} moedas** da poupança!\n" +
            $"Carteira: **{EconomyFormat.Full(wallet)}** | Poupança: **{EconomyFormat.Full(savings)}**");
    }

    [Command("trabalhar")]
    [Alias("work")]
    public async Task TrabalharAsync(string? servico = null)
    {
        if (string.IsNullOrWhiteSpace(servico))
        {
            var sb = new StringBuilder("💼 **Escolha um serviço:** `trabalhar <serviço>`\n\n");
            foreach (var item in EconomyJobs.All)
                sb.AppendLine($"{item.Emoji} **{item.Name}** (`{item.Key}`) — {item.Hours}h → +**{EconomyFormat.Full(item.TotalPay)}** moedas");
            await ReplyAsync(sb.ToString());
            return;
        }

        var job = EconomyJobs.Find(servico);
        if (job == null)
        {
            await ReplyAsync("❌ Serviço não encontrado. Use `trabalhar` para ver a lista.");
            return;
        }

        var now = DateTime.UtcNow;
        var profile = await _economy.GetOrCreateAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), Context.User.Username);

        if (EconomyRules.GetRemainingCooldown(profile.LastWorkTime, now, TimeSpan.FromHours(job.Hours)) is { } remaining)
        {
            await ReplyAsync($"⏳ Você ainda está trabalhando! Espere {FormatRemaining(remaining)} para trabalhar como **{job.Name}**.");
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var pay = job.TotalPay;
        var workPetBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Work);
        if (workPetBonus > 0)
            pay += pay * (ulong)workPetBonus / 100;

        var boost = profile.WorkBoostPending;
        if (boost && profile.WorkBoostExpiresAt is { } wExp && wExp <= DateTime.UtcNow)
        {
            await _economy.SetWorkBoostAsync(mainId, false);
            boost = false;
        }
        if (boost)
            pay *= 2;

        if (!await _economy.TryClaimWorkAsync(mainId, now, TimeSpan.FromHours(job.Hours)))
        {
            await ReplyAsync("⏳ Você já está trabalhando neste momento. Aguarde o término para começar outro serviço.");
            return;
        }

        await _economy.AddMoneyAsync(
            mainId, pay, EconomyTransactionType.Work,
            $"Trabalhou como {job.Name} ({job.Hours}h)");

        if (boost)
            await _economy.SetWorkBoostAsync(mainId, false);

        var boostSuffix = boost ? " ⚡ **(com bônus x2!)**" : string.Empty;
        await ReplyAsync($"💪 Você trabalhou como **{job.Emoji} {job.Name}** por {job.Hours}h e ganhou **{EconomyFormat.Full(pay)} moedas**!{boostSuffix}");
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
        var txns = await _economy.GetHistoryAsync(
            await _accessor.ResolveMainIdAsync(Context.User.Id), Math.Clamp(limite, 1, 30));

        if (txns.Count == 0)
        {
            await ReplyAsync("🧾 Você ainda não tem transações registradas.");
            return;
        }

        var sb = new StringBuilder("🧾 **Histórico de transações**\n\n");
        foreach (var t in txns)
        {
            var sinal = t.Amount >= 0 ? "+" : "";
            sb.AppendLine($"`{t.CreatedAt:dd/MM HH:mm}` **{t.Type}** {sinal}{EconomyFormat.Full((ulong)Math.Abs(t.Amount))} — {t.Description}");
        }

        await ReplyAsync(sb.ToString());
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