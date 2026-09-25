namespace GorillazDiscordBot.Entity;

public class GuildLogSettings
{
    public bool Enabled { get; set; }
    public ulong? ChannelId { get; set; }
    public bool LogMessageDeletes { get; set; } = true;
    public bool LogMessageEdits { get; set; } = true;
    public bool LogBans { get; set; } = true;
    public bool LogMembers { get; set; } = true;
}