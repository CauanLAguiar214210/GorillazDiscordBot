using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Commands.Slash;

public class GeneralSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Verifica se o bot está online")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong! 🏓");
    }

    [SlashCommand("saldo", "Ver seu saldo de moedas")]
    public async Task SaldoAsync()
    {
        await RespondAsync("💰 Use `macaco saldo` para ver seu saldo!");
    }
}
