using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopUpgradeTests
{
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();
    private readonly IShopRepository _shopRepo = Substitute.For<IShopRepository>();

    private readonly List<InventoryItem> _inventory = new();
    private List<ShopItem> _catalog = new();

    private static readonly ShopItem Vagas1 = new()
    {
        Key = "upgrade_vagas_1",
        Name = "Ampliação Simples",
        Emoji = "🅿️",
        Price = 12000,
        Category = ItemCategory.Upgrade,
        MaxQuantity = 3,
        UpgradeEffect = UpgradeEffect.ManobristaVagas,
        UpgradeValue = 2
    };

    private static readonly ShopItem Vagas2 = new()
    {
        Key = "upgrade_vagas_2",
        Name = "Ampliação Premium",
        Emoji = "🅿️",
        Price = 40000,
        Category = ItemCategory.Upgrade,
        MaxQuantity = 2,
        UpgradeEffect = UpgradeEffect.ManobristaVagas,
        UpgradeValue = 5
    };

    private static readonly ShopItem Base1 = new()
    {
        Key = "upgrade_base_1",
        Name = "Manutenção Leve",
        Emoji = "🔧",
        Price = 15000,
        Category = ItemCategory.Upgrade,
        MaxQuantity = 3,
        UpgradeEffect = UpgradeEffect.ManobristaBase,
        UpgradeValue = 10
    };

    private static readonly ShopItem Pet = new()
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

    public ShopUpgradeTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
        _accessor.ResolveMainIdAsync(1).Returns(1UL);
        _shopRepo.GetInventoryAsync(1).Returns(_ => _inventory);
    }

    private ShopService CreateService()
    {
        _shopRepo.GetAllAsync().Returns(_ => _catalog);
        var shop = new ShopService(_shopRepo, _economy, _accessor, Substitute.For<ICharacterProfileRepository>());
        shop.ForceReload();
        return shop;
    }

    private static InventoryItem Owned(ShopItem item, int qty, DateTime? expiresAt = null)
        => new()
        {
            UserId = 1,
            ItemKey = item.Key,
            Quantity = qty,
            AcquiredAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

    [Fact]
    public async Task GetUpgradeFlatAsync_SomaUnidadesVezesValorPorEfeito()
    {
        _catalog = new List<ShopItem> { Vagas1, Vagas2, Base1, Pet };
        _inventory.Add(Owned(Vagas1, 2));
        _inventory.Add(Owned(Vagas2, 1));
        _inventory.Add(Owned(Base1, 3));
        _inventory.Add(Owned(Pet, 5));
        var shop = CreateService();

        var vagas = await shop.GetUpgradeFlatAsync(2, UpgradeEffect.ManobristaVagas);
        var basePct = await shop.GetUpgradeFlatAsync(2, UpgradeEffect.ManobristaBase);

        vagas.Should().Be(2 * 2 + 5);
        basePct.Should().Be(30);
    }

    [Fact]
    public async Task GetUpgradeFlatAsync_IgnoraOutrasCategorias()
    {
        _catalog = new List<ShopItem> { Pet };
        _inventory.Add(Owned(Pet, 5));
        var shop = CreateService();

        var bonus = await shop.GetUpgradeFlatAsync(2, UpgradeEffect.Work);

        bonus.Should().Be(0);
    }

    [Fact]
    public async Task GetUpgradeFlatAsync_IgnoraItensExpirados()
    {
        _catalog = new List<ShopItem> { Vagas1 };
        _inventory.Add(Owned(Vagas1, 2, DateTime.UtcNow.AddHours(-1)));
        var shop = CreateService();

        var vagas = await shop.GetUpgradeFlatAsync(2, UpgradeEffect.ManobristaVagas);

        vagas.Should().Be(0);
    }

    [Fact]
    public async Task GetUpgradeFlatAsync_SemItens_RetornaZero()
    {
        _catalog = new List<ShopItem> { Vagas1 };
        var shop = CreateService();

        var vagas = await shop.GetUpgradeFlatAsync(2, UpgradeEffect.ManobristaVagas);

        vagas.Should().Be(0);
    }

    [Fact]
    public async Task GetUpgradePercentAsync_NaoContaItensDeMelhoria()
    {
        _catalog = new List<ShopItem> { Base1 };
        _inventory.Add(Owned(Base1, 3));
        var shop = CreateService();

        var pct = await shop.GetUpgradePercentAsync(2, UpgradeEffect.ManobristaBase);

        pct.Should().Be(0);
    }

    [Fact]
    public async Task BuyAsync_Melhoria_RespeitaMaxQuantity()
    {
        _catalog = new List<ShopItem> { Vagas1 };
        _inventory.Add(Owned(Vagas1, 3));
        _shopRepo.GetInventoryByKeyAsync(1, "upgrade_vagas_1").Returns(Owned(Vagas1, 3));
        var shop = CreateService();

        var (success, message, _) = await shop.BuyAsync(2, "alt", Vagas1);

        success.Should().BeFalse();
        message.Should().Contain("3");
        await _economy.DidNotReceive().TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }
}