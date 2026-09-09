using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopPetTests
{
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();
    private readonly IShopRepository _shopRepo = Substitute.For<IShopRepository>();

    private readonly List<InventoryItem> _inventory = new();
    private List<ShopItem> _catalog = new();

    private static readonly ShopItem Macaco = new()
    {
        Key = "pet_macaco",
        Name = "Macaco-Caçador",
        Emoji = "🐒",
        Price = 15000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.Daily,
        UpgradeValue = 2
    };

    private static readonly ShopItem Gorila = new()
    {
        Key = "pet_gorila",
        Name = "Gorila-Guarda-Costas",
        Emoji = "🦍",
        Price = 40000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.Work,
        UpgradeValue = 3
    };

    private static readonly ShopItem Fenix = new()
    {
        Key = "pet_fenix",
        Name = "Fênix",
        Emoji = "🐦‍🔥",
        Price = 100000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.AssetIncome,
        UpgradeValue = 4
    };

    public ShopPetTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
        _accessor.ResolveMainIdAsync(1).Returns(1UL);
        _shopRepo.GetInventoryAsync(1).Returns(_ => _inventory);
        _shopRepo.GetInventoryByKeyAsync(1, Arg.Any<string>()).Returns((InventoryItem?)null);
    }

    private ShopService CreateService()
    {
        _shopRepo.GetAllAsync().Returns(_ => _catalog);
        var shop = new ShopService(_shopRepo, _economy, _accessor);
        shop.ForceReload();
        return shop;
    }

    private static InventoryItem Owned(ShopItem item, int qty)
        => new() { UserId = 1, ItemKey = item.Key, Quantity = qty, AcquiredAt = DateTime.UtcNow };

    [Fact]
    public async Task GetUpgradePercentAsync_SomaNiveisVezesValorPorEfeito()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila, Fenix };
        _inventory.Add(Owned(Macaco, 5));
        _inventory.Add(Owned(Gorila, 2));
        _inventory.Add(Owned(Fenix, 3));
        var shop = CreateService();

        var daily = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Daily);
        var work = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Work);
        var asset = await shop.GetUpgradePercentAsync(2, UpgradeEffect.AssetIncome);
        var rob = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Rob);

        daily.Should().Be(10);
        work.Should().Be(6);
        asset.Should().Be(12);
        rob.Should().Be(0);
    }

    [Fact]
    public async Task GetUpgradePercentAsync_SemPets_RetornaZero()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila };
        var shop = CreateService();

        var bonus = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Daily);

        bonus.Should().Be(0);
    }

    [Fact]
    public async Task BuyAsync_ComDoisTipos_RecusaTerceiroTipo()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila, Fenix };
        _inventory.Add(Owned(Macaco, 1));
        _inventory.Add(Owned(Gorila, 2));
        var shop = CreateService();

        var (success, message, _) = await shop.BuyAsync(2, "alt", Fenix);

        success.Should().BeFalse();
        message.Should().Contain("2 tipos");
        await _economy.DidNotReceive().TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BuyAsync_ComDoisTipos_PermiteSubirNivelDoMesmo()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila, Fenix };
        _inventory.Add(Owned(Macaco, 4));
        _inventory.Add(Owned(Gorila, 1));
        _economy.TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns((true, 100UL));
        var shop = CreateService();

        var (success, _, _) = await shop.BuyAsync(2, "alt", Macaco);

        success.Should().BeTrue();
        await _shopRepo.Received(1).AddOrIncrementInventoryAsync(Arg.Is<InventoryItem>(i => i.ItemKey == "pet_macaco" && i.Quantity == 1));
    }

    [Fact]
    public async Task BuyAsync_ComUmTipo_SegundoTipoPermitido()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila, Fenix };
        _inventory.Add(Owned(Gorila, 1));
        _economy.TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns((true, 500UL));
        var shop = CreateService();

        var (success, _, balance) = await shop.BuyAsync(2, "alt", Macaco);

        success.Should().BeTrue();
        balance.Should().Be(500);
        await _shopRepo.Received(1).AddOrIncrementInventoryAsync(Arg.Any<InventoryItem>());
    }

    [Fact]
    public async Task ApplyAssetIncomesAsync_AplicaBonusDoPetDeAtivo()
    {
        _catalog = new List<ShopItem> { Fenix, Gorila };
        _inventory.Add(Owned(Fenix, 3));
        var fazenda = new ShopItem
        {
            Key = "fazenda",
            Name = "Fazenda Gorillaz",
            Price = 60000,
            Category = ItemCategory.Asset,
            DailyIncome = 1000,
            MaxQuantity = 1
        };
        _catalog.Insert(0, fazenda);
        _inventory.Add(new InventoryItem
        {
            UserId = 1,
            ItemKey = "fazenda",
            Quantity = 1,
            AcquiredAt = DateTime.UtcNow.AddDays(-2)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _shopRepo.UpdateIncomeTimestampAsync(1, "fazenda", Arg.Any<DateTime>()).Returns(Task.CompletedTask);
        var shop = CreateService();

        var (totalIncome, days, items) = await shop.ApplyAssetIncomesAsync(2, "alt");

        items.Should().Be(1);
        days.Should().Be(2);
        // renda base = 1000 * 2 dias = 2000; bônus do pet Fênix nível 3 = +12% → 2240
        totalIncome.Should().Be(2240);
    }
}