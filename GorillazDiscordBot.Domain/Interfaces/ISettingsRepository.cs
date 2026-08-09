namespace GorillazDiscordBot.Domain.Interfaces;

public interface ISettingsRepository<T> where T : class, IGuildSettings, new()
{
    Task<T> GetAsync(ulong guildId);
    Task SaveAsync(T settings);
    Task ResetAsync(ulong guildId);
}
