namespace GorillazDiscordBot.Entity;

public class CooldownSettings
{
    public bool Enabled { get; set; }
    public int DefaultSeconds { get; set; } = 5;
    public Dictionary<string, int> CommandSeconds { get; set; } = new();
}