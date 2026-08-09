using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class GuildVoiceSettings : IGuildSettings
{
    public ulong GuildId { get; set; }
    public ulong? CreatorChannelId { get; set; }
    public bool Enabled { get; set; }
}
