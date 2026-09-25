namespace GorillazDiscordBot.Entity;

public class SessaoSettings
{
    public bool Enabled { get; set; }

    public Dictionary<string, ulong> Channels { get; set; } = new();
}
