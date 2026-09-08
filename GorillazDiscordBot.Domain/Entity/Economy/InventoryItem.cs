namespace GorillazDiscordBot.Domain.Entity.Economy;

public class InventoryItem
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? AcquiredAt { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public bool IsEquipped { get; set; }
}
