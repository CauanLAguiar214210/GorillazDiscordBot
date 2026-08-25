using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class EconomyModule : ModuleBase<SocketCommandContext>
{
    private readonly IUserRepository _userRepository;

    public EconomyModule(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    [Command("daily")]
    public async Task DailyAsync()
    {
        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        var (claimed, newBalance) = await _userRepository.TryClaimDailyAsync(Context.User.Id);

        if (!claimed)
        {
            await ReplyAsync("⏰ Você já resgatou seu daily hoje! Volte amanhã.");
            return;
        }

        await ReplyAsync($"💰 **Daily resgatado!** +100 moedas na carteira. Saldo atual: **{newBalance}**");
    }

    [Command("saldo")]
    [Alias("carteira")]
    public async Task SaldoAsync()
    {
        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
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

        var (deducted, balanceAfterBet) = await _userRepository.TryDeductMoneyAsync(Context.User.Id, quantia);

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        bool win = Random.Shared.Next(2) == 0;

        if (win)
        {
            int premio = quantia * 2;
            await _userRepository.AddMoneyAsync(Context.User.Id, premio);
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

        var (deducted, _) = await _userRepository.TryDeductMoneyAsync(Context.User.Id, quantia);

        if (!deducted)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        await _userRepository.GetOrCreateAsync(receiver.Id, receiver.Username);
        await _userRepository.AddMoneyAsync(receiver.Id, quantia);

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

        var (success, wallet, bank) = await _userRepository.DepositAsync(Context.User.Id, quantia);

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

        var (success, wallet, bank) = await _userRepository.WithdrawAsync(Context.User.Id, quantia);

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
        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await ReplyAsync($"🏦 **{Context.User.GetDisplayName()}**\nCarteira: **{user.Money}** moedas\nBanco: **{user.Bank}** moedas");
    }

    [Command("ranking")]
    [Alias("rank")]
    public async Task RankingAsync()
    {
        var top = await _userRepository.GetTopUsersAsync(10);

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
