using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class CasinoModule : ModuleBase<SocketCommandContext>
{
    private readonly IUserRepository _userRepository;
    private readonly CasinoService _casino;

    public CasinoModule(IUserRepository userRepository, CasinoService casino)
    {
        _userRepository = userRepository;
        _casino = casino;
    }

    [Command("bet")]
    [Alias("apostar")]
    public async Task BetAsync(string valor)
    {
        var userId = Context.User.Id;
        int amount;

        if (valor is "all" or "tudo" or "allin")
        {
            var user = await _userRepository.GetOrCreateAsync(userId, Context.User.Username);
            if (user.Money <= 0)
            {
                await ReplyAsync("❌ Você não tem moedas na carteira para apostar tudo.");
                return;
            }

            amount = user.Money;
        }
        else if (!int.TryParse(valor, out amount) || amount <= 0)
        {
            await ReplyAsync("⚠️ Informe um valor numérico positivo ou `all` para apostar tudo.");
            return;
        }

        var result = await _casino.PlaceSimpleBetAsync(userId, amount);

        if (!result.Success)
        {
            await ReplyAsync(result.Error!);
            return;
        }

        var text = result.Won
            ? $"🎉 **Você ganhou!** +{amount} moedas! Saldo: **{result.Balance}**"
            : $"😢 **Você perdeu!** -{amount} moedas! Saldo: **{result.Balance}**";

        var components = Slash.CasinoSlashModule.CasinoTableBuilder.BuildReplayComponents("bet", amount.ToString());
        await ReplyAsync(text, components: components);
    }
}
