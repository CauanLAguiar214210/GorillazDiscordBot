using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services.Interfaces;

public interface IGifUrlService
{
    Task<string> GetDirectUrlAsync(string url);
    Task<string> GetDirectMediaUrlAsync(string url, GuildInteractionType tipo);
}