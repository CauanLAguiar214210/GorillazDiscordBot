using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Domain.Interfaces;

public interface IGuildVoiceRepository
{
    GuildVoiceSettings Get(ulong guildId);
    void Save(GuildVoiceSettings settings);
    void Reset(ulong guildId);
}