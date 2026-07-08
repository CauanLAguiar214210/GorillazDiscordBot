using System.Net.Http;
using System.Text.Json;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Services;

public class FOneService : IFOneService
{
    private readonly HttpClient _httpClient;

    public FOneService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DriverStanding>?> ObterClassificacaoPilotosAsync()
    {
        var json = await _httpClient.GetStringAsync("http://ergast.com/api/f1/current/driverStandings.json");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var ergastResponse = JsonSerializer.Deserialize<ErgastResponse>(json, options);

        var lista = ergastResponse?.MRData?.StandingsTable?.StandingsLists;
        return lista is { Count: > 0 } ? lista[0].DriverStandings : null;
    }
}
