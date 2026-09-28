namespace GorillazDiscordBot.Entity;

public class RaidSettings
{
    public bool Enabled { get; set; }
    public int MaxJoinsPerMinute { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}