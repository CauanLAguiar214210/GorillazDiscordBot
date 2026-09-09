namespace GorillazDiscordBot.Domain.Entity.Economy;

public enum ItemCategory
{
    Cosmetic,
    Boost,
    Asset,
    Relic,
    Pet
}

public enum BoostEffect
{
    None,
    DailyX2,
    WorkX2,
    RobShield
}

public enum UpgradeEffect
{
    None,
    Daily,
    Work,
    Rob,
    AssetIncome,
    Savings
}

public enum RelicEffect
{
    None,
    GainBonus,
    Cashback
}

public enum RelicGameType
{
    All,
    Roulette,
    Slots,
    Blackjack,
    Dice,
    Coin,
    Aviao,
    VideoPoker,
    Mines,
    Limbo,
    Rps,
    Race,
    Plinko,
    Wheel,
    HighLow,
    Baccarat
}

public class ShopItem
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ulong Price { get; set; }
    public ItemCategory Category { get; set; }
    public BoostEffect Effect { get; set; }
    public int DurationHours { get; set; }
    public ulong DailyIncome { get; set; }
    public int MaxQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPlaceholder { get; set; }
    public int SortOrder { get; set; }

    public RelicEffect RelicEffect { get; set; }
    public RelicGameType RelicGame { get; set; }
    public int RelicValue { get; set; }

    public UpgradeEffect UpgradeEffect { get; set; }
    public int UpgradeValue { get; set; }
}
