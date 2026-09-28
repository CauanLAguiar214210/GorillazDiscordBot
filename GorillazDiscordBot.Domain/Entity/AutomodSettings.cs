namespace GorillazDiscordBot.Entity;

public enum AutomodAction
{
    Delete,
    Timeout
}

public class AutomodSettings
{
    public bool Enabled { get; set; }
    public AutomodAction Action { get; set; } = AutomodAction.Delete;
    public int MaxMessagesPerInterval { get; set; } = 5;
    public int IntervalSeconds { get; set; } = 10;
    public int TimeoutMinutes { get; set; } = 10;
    public List<string> BlockedWords { get; set; } = new();
    public bool BlockInvites { get; set; }
    public bool BlockEveryonePings { get; set; }
    public int MaxMentionsPerMessage { get; set; }
    public bool EnableStrikes { get; set; }
    public int StrikeTimeoutWarnings { get; set; } = 3;
    public int StrikeBanWarnings { get; set; } = 6;
}