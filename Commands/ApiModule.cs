using System.Text;
using Discord.Commands;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Commands;

public class ApiModule : ModuleBase<SocketCommandContext>
{
    private readonly IFOneService _f1Service;
    private readonly ICotacaoService _cotacaoService;
    private readonly IWeatherService _weatherService;

    public ApiModule(IFOneService f1Service, ICotacaoService cotacaoService, IWeatherService weatherService)
    {
        _f1Service = f1Service;
        _cotacaoService = cotacaoService;
        _weatherService = weatherService;
    }

    [Command("f1")]
    public async Task ObterClassificacaoPilotosAsync()
    {
        try
        {
            var pilotos = await _f1Service.ObterClassificacaoPilotosAsync();

            if (pilotos != null)
            {
                foreach (var p in pilotos)
                    await ReplyAsync($"{p.Position} - {p.Driver.GivenName} {p.Driver.FamilyName} - {p.Points} pontos");
            }
            else
            {
                await ReplyAsync("Não foi possível obter a classificação.");
            }
        }
        catch(Exception ex)
        {
            await ReplyAsync("Falha na Integração com a FO.");
        }
    }

    [Command("cotacao")]
    public async Task CotacaoAsync()
    {
        var moedas = new[] { "USD-BRL", "EUR-BRL", "GBP-BRL", "ARS-BRL" };
        var nomesCompletos = new Dictionary<string, string>
        {
            { "USD-BRL", "Dólar Americano" },
            { "EUR-BRL", "Euro" },
            { "GBP-BRL", "Libra Esterlina" },
            { "ARS-BRL", "Peso Argentino" }
        };

        var cotacoes = await _cotacaoService.ObterCotacoesAsync(moedas);
        var sb = new StringBuilder("💰 **Cotação das Moedas:**\n");

        foreach (var par in cotacoes)
        {
            var nome = nomesCompletos.GetValueOrDefault(par.Key, par.Key);
            sb.AppendLine(par.Value.HasValue
                ? $"{nome}: R$ {par.Value.Value:F2}"
                : $"{nome}: cotação indisponível");
        }

        await ReplyAsync(sb.ToString());
    }

    [Command("tempo")]
    public async Task TempoAsync([Remainder] string cidade)
    {
        if (string.IsNullOrWhiteSpace(cidade))
        {
            await ReplyAsync("🌤️ Use: `macaco tempo <cidade>`");
            return;
        }

        var resultado = await _weatherService.GetWeatherAsync(cidade);
        await ReplyAsync(resultado);
    }
}
