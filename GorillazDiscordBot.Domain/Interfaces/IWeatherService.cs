namespace GorillazDiscordBot.Services.Interfaces;

public interface IWeatherService
{
    Task<string?> GetWeatherAsync(string cidade);
}
