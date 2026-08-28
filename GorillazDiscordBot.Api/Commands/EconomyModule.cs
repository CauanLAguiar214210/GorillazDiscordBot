using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Economy;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class EconomyModule : ModuleBase<SocketCommandContext>
{
    private readonly IEconomyRepository _economy;

    public EconomyModule(IEconomyRepository economy)
    {
        _economy = economy;
    }

    [Command("daily")]
    public async Task DailyAsync()
    {
        var reward = EconomyRules.GetDailyReward(Random.Shared);
        var (claimed, newBalance) = await _economy.TryClaimDailyAsync(Context.User.Id, reward);

        if (!claimed)
        {
            await ReplyAsync("⏰ Você já resgatou seu daily hoje! Volte amanhã.");
            return;
        }

        await ReplyAsync($"💰 **Daily resgatado!** +{reward} moedas na carteira. Saldo atual: **{newBalance}**");
    }

    [Command("saldo")]
    [Alias("carteira")]
    public async Task SaldoAsync()
    {
        var user = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await ReplyAsync($"💰 **{Context.User.GetDisplayName()}**, seu saldo é **{user.Money} moedas** na carteira.");
    }

    [Command("bet")]
    public async Task BetAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (deducted, balanceAfterBet) = await _economy.TryDeductMoneyAsync(
            Context.User.Id, quantia, EconomyTransactionType.Bet, "Aposta");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        bool win = Random.Shared.Next(2) == 0;

        if (win)
        {
            int premio = quantia * 2;
            await _economy.AddMoneyAsync(Context.User.Id, premio, EconomyTransactionType.Bet, "Aposta ganha");
            await ReplyAsync($"🎉 **Você ganhou!** +{quantia} moedas! Saldo: **{balanceAfterBet + premio}**");
        }
        else
        {
            await ReplyAsync($"😢 **Você perdeu!** -{quantia} moedas! Saldo: **{balanceAfterBet}**");
        }
    }

    [Command("pagar")]
    [Alias("pay")]
    public async Task PagarAsync(IUser receiver, int quantia)
    {
        if (quantia <= 0)
        {
            await ReplyAsync("⚠️ A quantia deve ser positiva.");
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

        var (deducted, _) = await _economy.TryDeductMoneyAsync(
            Context.User.Id, quantia, EconomyTransactionType.Payment,
            $"Pagamento para {receiver.GetDisplayName()}");

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        await _economy.GetOrCreateAsync(receiver.Id, receiver.Username);
        await _economy.AddMoneyAsync(receiver.Id, quantia, EconomyTransactionType.Payment,
            $"Pagamento de {Context.User.GetDisplayName()}");

        await ReplyAsync($"💸 **{Context.User.GetDisplayName()}** pagou **{quantia} moedas** para **{receiver.GetDisplayName()}**!");
    }

    [Command("depositar")]
    [Alias("dep", "deposit")]
    public async Task DepositarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, bank) = await _economy.DepositAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para depositar.");
            return;
        }

        await ReplyAsync($"🏦 **{Context.User.GetDisplayName()}** depositou **{quantia} moedas** no banco!\nCarteira: **{wallet}** | Banco: **{bank}**");
    }

    [Command("sacar")]
    [Alias("withdraw", "wd")]
    public async Task SacarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, bank) = await _economy.WithdrawAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes no banco para sacar.");
            return;
        }

        await ReplyAsync($"🏧 **{Context.User.GetDisplayName()}** sacou **{quantia} moedas** do banco!\nCarteira: **{wallet}** | Banco: **{bank}**");
    }

    [Command("banco")]
    [Alias("bank", "banksaldo")]
    public async Task BancoAsync()
    {
        var user = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await ReplyAsync($"🏦 **{Context.User.GetDisplayName()}**\nCarteira: **{user.Money}** moedas\nBanco: **{user.Bank}** moedas");
    }

    [Command("poupanca")]
    [Alias("savings")]
    public async Task PoupancaAsync()
    {
        var user = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        var min = EconomyRules.DailyInterestMin + Math.Min(user.SavingsStreak, EconomyRules.InterestStreakMaxBonus) * EconomyRules.InterestStreakBonus;
        var max = EconomyRules.DailyInterestMax + Math.Min(user.SavingsStreak, EconomyRules.InterestStreakMaxBonus) * EconomyRules.InterestStreakBonus;

        await ReplyAsync(
            $"🏦 **{Context.User.GetDisplayName()}**\n" +
            $"Carteira: **{user.Money}**\nBanco: **{user.Bank}**\n" +
            $"Poupança: **{user.Savings}** (streak: **{user.SavingsStreak}**)\n" +
            $"Juros diários: **{min:P1}–{max:P1}**");
    }

    [Command("poupar")]
    [Alias("savingsdeposit")]
    public async Task PouparAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.DepositSavingsAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para poupar.");
            return;
        }

        await ReplyAsync(
            $"🏦 **{Context.User.GetDisplayName()}** depositou **{quantia} moedas** na poupança!\n" +
            $"Carteira: **{wallet}** | Poupança: **{savings}** | Streak: **{streak}**");
    }

    [Command("resgatar")]
    [Alias("savingswithdraw")]
    public async Task ResgatarAsync(string valor)
    {
        if (!EconomyHelper.TryParsePositiveAmount(valor, out int quantia, out var error))
        {
            await ReplyAsync(error!);
            return;
        }

        var (success, wallet, savings, streak) = await _economy.WithdrawSavingsAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na poupança para resgatar.");
            return;
        }

        await ReplyAsync(
            $"🏧 **{Context.User.GetDisplayName()}** resgatou **{quantia} moedas** da poupança!\n" +
            $"Carteira: **{wallet}** | Poupança: **{savings}**");
    }

    [Command("trabalhar")]
    [Alias("work")]
    public async Task TrabalharAsync(string? servico = null)
    {
        if (string.IsNullOrWhiteSpace(servico))
        {
            var sb = new StringBuilder("💼 **Escolha um serviço:** `trabalhar <serviço>`\n\n");
            foreach (var item in EconomyJobs.All)
                sb.AppendLine($"{item.Emoji} **{item.Name}** (`{item.Key}`) — {item.Hours}h → +**{item.TotalPay}** moedas");
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
        var profile = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        if (EconomyRules.GetRemainingCooldown(profile.LastWorkTime, now, TimeSpan.FromHours(job.Hours)) is { } remaining)
        {
            await ReplyAsync($"⏳ Você ainda está trabalhando! Espere {FormatRemaining(remaining)} para trabalhar como **{job.Name}**.");
            return;
        }

        await _economy.AddMoneyAsync(Context.User.Id, job.TotalPay, EconomyTransactionType.Work,
            $"Trabalhou como {job.Name} ({job.Hours}h)");
        await _economy.SetLastWorkAsync(Context.User.Id, now);

        await ReplyAsync($"💪 Você trabalhou como **{job.Emoji} {job.Name}** por {job.Hours}h e ganhou **{job.TotalPay} moedas**!");
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

        var now = DateTime.UtcNow;
        var attacker = await _economy.GetOrCreateAsync(Context.User.Id, Context.User.Username);

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

        var victim = await _economy.GetOrCreateAsync(target.Id, target.Username);

        if (victim.Money < 1)
        {
            await ReplyAsync("😅 Essa pessoa está sem moedas na carteira.");
            return;
        }

        if (EconomyRules.ShouldRobSucceed(Random.Shared))
        {
            int stolen = EconomyRules.ComputeRobAmount(victim.Money, Random.Shared);

            await _economy.TryDeductMoneyAsync(target.Id, stolen, EconomyTransactionType.Rob,
                $"Roubado por {Context.User.GetDisplayName()}");
            await _economy.AddMoneyAsync(Context.User.Id, stolen, EconomyTransactionType.Rob,
                $"Roubou {stolen} moedas de {target.GetDisplayName()}");
            await _economy.SetRobAttemptAsync(Context.User.Id, now, null);

            await ReplyAsync($"🕵️ **Você roubou {stolen} moedas** de **{target.GetDisplayName()}**!");
        }
        else
        {
            await _economy.SetRobAttemptAsync(Context.User.Id, now, now.Add(EconomyRules.RobCaughtLockout));
            await ReplyAsync($"🚨 **Você foi pego roubando** **{target.GetDisplayName()}**! Ficará **3 horas** sem poder roubar.");
        }
    }

    [Command("ranking")]
    [Alias("rank")]
    public async Task RankingAsync()
    {
        var top = await _economy.GetTopUsersAsync(10);

        if (top.Count == 0)
        {
            await ReplyAsync("📭 Ninguém tem moedas ainda.");
            return;
        }

        var sb = new StringBuilder("🏆 **Ranking de Riqueza**\n\n");
        int pos = 1;

        foreach (var u in top)
        {
            var medal = pos switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"{pos}º"
            };
            sb.AppendLine($"{medal} **{u.Username}** — {u.Money} moedas");
            pos++;
        }

        await ReplyAsync(sb.ToString());
    }

    [Command("historico")]
    [Alias("extrato")]
    public async Task HistoricoAsync(int limite = 10)
    {
        var txns = await _economy.GetHistoryAsync(Context.User.Id, Math.Clamp(limite, 1, 30));

        if (txns.Count == 0)
        {
            await ReplyAsync("🧾 Você ainda não tem transações registradas.");
            return;
        }

        var sb = new StringBuilder("🧾 **Histórico de transações**\n\n");
        foreach (var t in txns)
        {
            var sinal = t.Amount >= 0 ? "+" : "";
            sb.AppendLine($"`{t.CreatedAt:dd/MM HH:mm}` **{t.Type}** {sinal}{t.Amount} — {t.Description}");
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
        public static bool TryParsePositiveAmount(string input, out int amount, out string? error)
        {
            amount = 0;
            error = null;

            if (!int.TryParse(input, out amount))
            {
                error = "⚠️ Informe um valor numérico. Exemplo: `bet 100`";
                return false;
            }

            if (amount <= 0)
            {
                error = "⚠️ O valor deve ser positivo.";
                return false;
            }

            return true;
        }

        public static async Task<bool> TryParseAndReplyAsync(string input, ICommandContext context)
        {
            if (!TryParsePositiveAmount(input, out _, out var error))
            {
                await context.Channel.SendMessageAsync(error!);
                return false;
            }
            return true;
        }
    }
}