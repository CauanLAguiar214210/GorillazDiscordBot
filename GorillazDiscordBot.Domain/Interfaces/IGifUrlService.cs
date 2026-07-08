namespace GorillazDiscordBot.Services.Interfaces;

public interface IGifUrlService
{
    Task<string> GetDirectUrlAsync(string url);
}
