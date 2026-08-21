using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class GuildVoicePresenceSettings : IGuildSettings
{
    public ulong GuildId { get; set; }
    public ulong? VoiceChannelId { get; set; }
    public bool Enabled { get; set; }
}
