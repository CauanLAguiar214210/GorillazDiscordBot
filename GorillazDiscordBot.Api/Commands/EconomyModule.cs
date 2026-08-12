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

        await ReplyAsync($"💰 **Daily resgatado!** +100 moedas. Saldo atual: **{newBalance}**");
    }

    [Command("saldo")]
    [Alias("carteira")]
    public async Task SaldoAsync()
    {
        var user = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
        await ReplyAsync($"💰 **{Context.User.Username}**, seu saldo é **{user.Points} moedas**.");
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

        if (user.Points < quantia)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes.");
            return;
        }

        bool win = _random.Next(2) == 0;

        if (win)
        {
            await _userRepository.AddPointsAsync(Context.User.Id, quantia);
            var updated = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
            await ReplyAsync($"🎉 **Você ganhou!** +{quantia} moedas! Saldo: **{updated.Points}**");
        }
        else
        {
            await _userRepository.AddPointsAsync(Context.User.Id, -quantia);
            var updated = await _userRepository.GetOrCreateAsync(Context.User.Id, Context.User.Username);
            await ReplyAsync($"😢 **Você perdeu!** -{quantia} moedas! Saldo: **{updated.Points}**");
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

        if (sender.Points < quantia)
        {
            await ReplyAsync("❌ Você não tem moedas suficientes.");
            return;
        }

        await _userRepository.AddPointsAsync(Context.User.Id, -quantia);
        await _userRepository.GetOrCreateAsync(receiver.Id, receiver.Username);
        await _userRepository.AddPointsAsync(receiver.Id, quantia);

        await ReplyAsync($"💸 **{Context.User.Username}** pagou **{quantia} moedas** para **{receiver.Username}**!");
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
            sb.AppendLine($"{medal} **{u.Username}** — {u.Points} moedas");
            pos++;
        }

        await ReplyAsync(sb.ToString());
    }
}
