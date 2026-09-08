using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class Guild : IGuildSettings
{
    public ulong GuildId { get; set; }
    public GuildInfo Info { get; set; }
    public PrefixSettings Prefix { get; set; }
    public WelcomeSettings Welcome { get; set; }
    public List<VoiceChannelSettings> VoiceChannels { get; set; }

    public Guild()
    {
        Info = new GuildInfo();
        Prefix = new PrefixSettings();
        Welcome = new WelcomeSettings();
        VoiceChannels = new List<VoiceChannelSettings>();
    }
}