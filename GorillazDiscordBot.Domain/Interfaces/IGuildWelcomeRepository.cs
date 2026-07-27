using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGuildWelcomeRepository
{
    GuildWelcomeSettings Get(ulong guildId);
    void Save(GuildWelcomeSettings settings);
    void Reset(ulong guildId);
}
