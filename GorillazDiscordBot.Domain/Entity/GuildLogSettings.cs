namespace GorillazDiscordBot.Entity;

public class GuildLogSettings
{
    public bool Enabled { get; set; }
    public ulong? ChannelId { get; set; }
    public bool LogMessageDeletes { get; set; } = true;
    public bool LogMessageEdits { get; set; } = true;
    public bool LogBans { get; set; } = true;
    public bool LogMembers { get; set; } = true;
    public bool LogKicks { get; set; } = true;
    public bool LogTimeouts { get; set; } = true;
    public bool LogWarnings { get; set; } = true;
    public bool LogBulkDeletes { get; set; } = true;
    public bool LogNicknameChanges { get; set; } = true;
    public bool LogRoleChanges { get; set; } = true;
    public bool LogVoice { get; set; } = false;
    public bool LogChannelChanges { get; set; } = false;
}