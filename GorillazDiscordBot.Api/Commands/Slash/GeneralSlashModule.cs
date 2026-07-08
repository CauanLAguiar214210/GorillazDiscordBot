using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Commands.Slash;

public class GeneralSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IWeatherService _weatherService;

    public GeneralSlashModule(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [SlashCommand("ping", "Verifica se o bot está online")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong! 🏓");
    }

    [SlashCommand("tempo", "Previsão do tempo para uma cidade")]
    public async Task TempoAsync([Summary("cidade", "Nome da cidade")] string cidade)
    {
        await DeferAsync();
        var resultado = await _weatherService.GetWeatherAsync(cidade);
        await FollowupAsync(resultado);
    }

    [SlashCommand("saldo", "Ver seu saldo de moedas")]
    public async Task SaldoAsync()
    {
        await RespondAsync("💰 Use `macaco saldo` para ver seu saldo!");
    }
}
