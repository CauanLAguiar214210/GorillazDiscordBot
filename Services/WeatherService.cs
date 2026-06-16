using System.Net.Http;
using System.Text.Json;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Services;

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public WeatherService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("OWM_API_KEY") ?? "";
    }

    public async Task<string?> GetWeatherAsync(string cidade)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return "⚠️ Chave da API OpenWeatherMap não configurada (OWM_API_KEY).";

        var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cidade)}&appid={_apiKey}&units=metric&lang=pt_br";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"🌤️ Cidade `{cidade}` não encontrada."
                    : $"⚠️ Erro ao consultar clima: {(int)response.StatusCode}";

            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            var name = root.GetProperty("name").GetString();
            var main = root.GetProperty("main");
            var temp = main.GetProperty("temp").GetDouble();
            var feelsLike = main.GetProperty("feels_like").GetDouble();
            var humidity = main.GetProperty("humidity").GetInt32();
            var weather = root.GetProperty("weather")[0];
            var description = weather.GetProperty("description").GetString();
            var icon = weather.GetProperty("icon").GetString();
            var wind = root.GetProperty("wind");
            var windSpeed = wind.GetProperty("speed").GetDouble();
            var country = root.GetProperty("sys").GetProperty("country").GetString();

            var emoji = icon?[..2] switch
            {
                "01" => "☀️",
                "02" => "⛅",
                "03" => "☁️",
                "04" => "☁️",
                "09" => "🌧️",
                "10" => "🌦️",
                "11" => "⛈️",
                "13" => "❄️",
                "50" => "🌫️",
                _ => "🌡️"
            };

            return $"{emoji} **{name}, {country}**\n" +
                   $"🌡️ {temp:F1}°C (sensação {feelsLike:F1}°C)\n" +
                   $"☁️ {description}\n" +
                   $"💧 Umidade: {humidity}%\n" +
                   $"💨 Vento: {windSpeed:F1} m/s";
        }
        catch (HttpRequestException)
        {
            return "⚠️ Não foi possível conectar ao serviço de clima.";
        }
        catch (JsonException)
        {
            return "⚠️ Resposta inválida do serviço de clima.";
        }
    }
}
