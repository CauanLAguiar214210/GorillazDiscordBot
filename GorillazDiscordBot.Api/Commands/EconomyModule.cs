using System.Text;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Commands;

public class EconomyModule : ModuleBase<SocketCommandContext>
{
    private readonly IUserRepository _userRepository;
    private static readonly Random _random = new();

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
        await ReplyAsync($"💰 **{Context.User.Username}**, seu saldo é **{user.Money} moedas** na carteira.");
    }

    [Command("bet")]
    public async Task BetAsync(string valor)
    {
        if (!int.TryParse(valor, out int quantia))
        {
            await ReplyAsync("⚠️ Informe um valor numérico Inteiro. Exemplo: `bet 100`");
            return;
        }

        if (quantia <= 0)
        {
            await ReplyAsync("⚠️ A aposta deve ser um valor positivo.");
            return;
        }

        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        if (user.Money < quantia)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        bool win = _random.Next(2) == 0;

        if (win)
        {
            await _userRepository.AddMoneyAsync(Context.User.Id, quantia);
            var updated = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
            await ReplyAsync($"🎉 **Você ganhou!** +{quantia} moedas! Saldo: **{updated.Money}**");
        }
        else
        {
            await _userRepository.AddMoneyAsync(Context.User.Id, -quantia);
            var updated = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
            await ReplyAsync($"😢 **Você perdeu!** -{quantia} moedas! Saldo: **{updated.Money}**");
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

        var sender = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);

        if (sender.Money < quantia)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira.");
            return;
        }

        await _userRepository.AddMoneyAsync(Context.User.Id, -quantia);
        await _userRepository.GetOrCreateAsync(receiver.Id, receiver.Username);
        await _userRepository.AddMoneyAsync(receiver.Id, quantia);

        await ReplyAsync($"💸 **{Context.User.Username}** pagou **{quantia} moedas** para **{receiver.Username}**!");
    }

    [Command("depositar")]
    [Alias("dep", "deposit")]
    public async Task DepositarAsync(string valor)
    {
        if (!int.TryParse(valor, out int quantia))
        {
            await ReplyAsync("⚠️ Informe um valor numérico. Exemplo: `depositar 100`");
            return;
        }

        if (quantia <= 0)
        {
            await ReplyAsync("⚠️ O valor deve ser positivo.");
            return;
        }

        var (success, wallet, bank) = await _userRepository.DepositAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes na carteira para depositar.");
            return;
        }

        await ReplyAsync($"🏦 **{Context.User.Username}** depositou **{quantia} moedas** no banco!\nCarteira: **{wallet}** | Banco: **{bank}**");
    }

    [Command("sacar")]
    [Alias("withdraw", "wd")]
    public async Task SacarAsync(string valor)
    {
        if (!int.TryParse(valor, out int quantia))
        {
            await ReplyAsync("⚠️ Informe um valor numérico. Exemplo: `sacar 100`");
            return;
        }

        if (quantia <= 0)
        {
            await ReplyAsync("⚠️ O valor deve ser positivo.");
            return;
        }

        var (success, wallet, bank) = await _userRepository.WithdrawAsync(Context.User.Id, quantia);

        if (!success)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes no banco para sacar.");
            return;
        }

        await ReplyAsync($"🏧 **{Context.User.Username}** sacou **{quantia} moedas** do banco!\nCarteira: **{wallet}** | Banco: **{bank}**");
    }

    [Command("banco")]
    [Alias("bank", "banksaldo")]
    public async Task BancoAsync()
    {
        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await ReplyAsync($"🏦 **{Context.User.Username}**\nCarteira: **{user.Money}** moedas\nBanco: **{user.Bank}** moedas");
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
}
