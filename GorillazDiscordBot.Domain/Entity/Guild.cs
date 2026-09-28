using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Entity;

public class Guild : IGuildSettings
{
    public ulong GuildId { get; set; }
    public GuildInfo Info { get; set; }
    public PrefixSettings Prefix { get; set; }
public PanelSettings Panel { get; set; }

public SessaoSettings Sessao { get; set; }
    public WelcomeSettings Welcome { get; set; }
    public ReleaseSettings Release { get; set; }
    public List<VoiceChannelSettings> VoiceChannels { get; set; }
    public List<ScheduledSoundSettings> ScheduledSounds { get; set; }
    public string? JoinVoiceSound { get; set; }
    public string? LeaveVoiceSound { get; set; }
    public AutomodSettings Automod { get; set; }
    public GuildLogSettings Logs { get; set; }
    public TicketSettings Ticket { get; set; }
    public CooldownSettings Cooldown { get; set; }
    public PermissionSettings Permission { get; set; }

    public Guild()
    {
        Info = new GuildInfo();
        Prefix = new PrefixSettings();
        Panel = new PanelSettings();
        Sessao = new SessaoSettings();
        Welcome = new WelcomeSettings();
        Release = new ReleaseSettings();
        VoiceChannels = new List<VoiceChannelSettings>();
        ScheduledSounds = new List<ScheduledSoundSettings>();
        Automod = new AutomodSettings();
        Logs = new GuildLogSettings();
        Ticket = new TicketSettings();
        Cooldown = new CooldownSettings();
        Permission = new PermissionSettings();
    }
}