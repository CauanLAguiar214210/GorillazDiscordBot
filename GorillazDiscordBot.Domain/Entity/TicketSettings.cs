namespace GorillazDiscordBot.Entity;

public class TicketSettings
{
    public bool Enabled { get; set; }
    public ulong? CategoryId { get; set; }
    public ulong? SupportRoleId { get; set; }
}