namespace GorillazDiscordBot.Entity;

public class GuildVoiceSettings
{
    public ulong GuildId { get; set; }
    public ulong? CreatorChannelId { get; set; }
    public bool Enabled { get; set; }
}
