using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Service
{
    internal class FOneService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<List<DriverStanding>> ObterClassificacaoPilotosAsync()
        {
            string url = "http://ergast.com/api/f1/current/driverStandings.json";

            var json = await _httpClient.GetStringAsync(url);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var ergastResponse = JsonSerializer.Deserialize<ErgastResponse>(json, options);

            var lista = ergastResponse?.MRData?.StandingsTable?.StandingsLists;

            if (lista != null && lista.Count > 0)
                return lista[0].DriverStandings;

            return null;
        }
    }
}
