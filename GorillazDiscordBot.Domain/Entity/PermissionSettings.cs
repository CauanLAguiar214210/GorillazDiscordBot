namespace GorillazDiscordBot.Entity;

public class PermissionEntry
{
    public string Command { get; set; } = string.Empty;
    public ulong RoleId { get; set; }
    public bool Allowed { get; set; }
}

public class PermissionSettings
{
    public bool Enabled { get; set; }
    public List<PermissionEntry> Entries { get; set; } = new();
}