namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum EconomyTransactionType
{
    Daily,
    Bet,
    Payment,
    Deposit,
    Withdraw,
    SavingsDeposit,
    SavingsWithdraw,
    Interest,
    Work,
    Rob,
    Tax
}

public class EconomyTransaction
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public EconomyTransactionType Type { get; set; }
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}