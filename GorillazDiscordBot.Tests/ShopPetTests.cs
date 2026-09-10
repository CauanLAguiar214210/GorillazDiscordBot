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

    private static readonly ShopItem EvolvedMacaco = new()
    {
        Key = "pet_macaco",
        Name = "Macaco-Caçador",
        Emoji = "🐒",
        Price = 15000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.Daily,
        UpgradeValue = 2,
        EvolvedName = "Macaco-Alfa",
        EvolvedEmoji = "👑",
        EvolvedUpgradeValue = 4
    };

    private static readonly ShopItem Pet2D = new()
    {
        Key = "pet_2d",
        Name = "2D Sortudo",
        Emoji = "🎤",
        Price = 20000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.Casino,
        UpgradeValue = 2
    };

    private static readonly ShopItem PetRussel = new()
    {
        Key = "pet_russel",
        Name = "Russell Sentinela",
        Emoji = "🥁",
        Price = 30000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.RobDefense,
        UpgradeValue = 3
    };

    private static readonly ShopItem PetChaves = new()
    {
        Key = "pet_chaves",
        Name = "Murdoc Banqueiro",
        Emoji = "💸",
        Price = 25000,
        Category = ItemCategory.Pet,
        MaxQuantity = 10,
        UpgradeEffect = UpgradeEffect.Savings,
        UpgradeValue = 2
    };

    private static readonly ShopItem PetUpMacaco = new()
    {
        Key = "petup_macaco",
        Name = "Banana da Sabedoria",
        Emoji = "🍌",
        Description = "Sobe +1 nível do Macaco-Caçador.",
        Price = 4000,
        Category = ItemCategory.Consumable,
        IsActive = true,
        TargetPetKey = "pet_macaco"
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
        var casino = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Casino);
        var robDefense = await shop.GetUpgradePercentAsync(2, UpgradeEffect.RobDefense);
        var savings = await shop.GetUpgradePercentAsync(2, UpgradeEffect.Savings);

        daily.Should().Be(10);
        work.Should().Be(6);
        asset.Should().Be(12);
        rob.Should().Be(0);
        casino.Should().Be(0);
        robDefense.Should().Be(0);
        savings.Should().Be(0);
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
    public async Task BuyAsync_PetJaAdotado_RecusaCompraDuplicada()
    {
        _catalog = new List<ShopItem> { Macaco, Gorila, PetUpMacaco };
        _inventory.Add(Owned(Macaco, 4));
        _inventory.Add(Owned(Gorila, 1));
        var shop = CreateService();

        var (success, message, _) = await shop.BuyAsync(2, "alt", Macaco);

        success.Should().BeFalse();
        message.Should().Contain("já adotou");
        message.Should().Contain("Banana da Sabedoria");
        await _economy.DidNotReceive().TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UseAsync_Consumivel_SobeNivelDoPetEConsome()
    {
        _catalog = new List<ShopItem> { EvolvedMacaco, PetUpMacaco };
        _inventory.Add(Owned(EvolvedMacaco, 4));
        _inventory.Add(Owned(PetUpMacaco, 2));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(EvolvedMacaco, 4));
        _shopRepo.GetInventoryByKeyAsync(1, "petup_macaco").Returns(Owned(PetUpMacaco, 2));
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 500 });
        var shop = CreateService();

        var (success, message, balance) = await shop.UseAsync(2, "alt", PetUpMacaco);

        success.Should().BeTrue();
        balance.Should().Be(500);
        message.Should().Contain("nível 5/10");
        message.Should().Contain("bônus total **10%**");
        await _shopRepo.Received(1).DecrementOrRemoveInventoryAsync(1, "petup_macaco");
        await _shopRepo.Received(1).AddOrIncrementInventoryAsync(
            Arg.Is<InventoryItem>(i => i.ItemKey == "pet_macaco" && i.Quantity == 1));
        await _economy.Received(1).LogTransactionAsync(1, EconomyTransactionType.BoostUse, 0, Arg.Any<string>());
    }

    [Fact]
    public async Task UseAsync_Consumivel_PetNaoAdotado_Recusa()
    {
        _catalog = new List<ShopItem> { Macaco, PetUpMacaco };
        _inventory.Add(Owned(PetUpMacaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "petup_macaco").Returns(Owned(PetUpMacaco, 1));
        var shop = CreateService();

        var (success, message, _) = await shop.UseAsync(2, "alt", PetUpMacaco);

        success.Should().BeFalse();
        message.Should().Contain("ainda não adotou");
        await _shopRepo.DidNotReceive().DecrementOrRemoveInventoryAsync(1, "petup_macaco");
    }

    [Fact]
    public async Task UseAsync_Consumivel_SemConsumivel_Recusa()
    {
        _catalog = new List<ShopItem> { Macaco, PetUpMacaco };
        _inventory.Add(Owned(Macaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(Macaco, 1));
        var shop = CreateService();

        var (success, message, _) = await shop.UseAsync(2, "alt", PetUpMacaco);

        success.Should().BeFalse();
        message.Should().Contain("Compre na loja");
        await _shopRepo.DidNotReceive().DecrementOrRemoveInventoryAsync(1, "petup_macaco");
    }

    [Fact]
    public async Task UseAsync_Consumivel_NivelMaximo_RecusaENaoConsome()
    {
        _catalog = new List<ShopItem> { EvolvedMacaco, PetUpMacaco };
        _inventory.Add(Owned(EvolvedMacaco, 10));
        _inventory.Add(Owned(PetUpMacaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(EvolvedMacaco, 10));
        _shopRepo.GetInventoryByKeyAsync(1, "petup_macaco").Returns(Owned(PetUpMacaco, 1));
        var shop = CreateService();

        var (success, message, _) = await shop.UseAsync(2, "alt", PetUpMacaco);

        success.Should().BeFalse();
        message.Should().Contain("nível máximo");
        await _shopRepo.DidNotReceive().DecrementOrRemoveInventoryAsync(1, "petup_macaco");
    }

    [Fact]
    public async Task UseAsync_Consumivel_EvolucionaNoNivelMaximo()
    {
        _catalog = new List<ShopItem> { EvolvedMacaco, PetUpMacaco };
        _inventory.Add(Owned(EvolvedMacaco, 9));
        _inventory.Add(Owned(PetUpMacaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(EvolvedMacaco, 9));
        _shopRepo.GetInventoryByKeyAsync(1, "petup_macaco").Returns(Owned(PetUpMacaco, 1));
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 200 });
        var shop = CreateService();

        var (success, message, _) = await shop.UseAsync(2, "alt", PetUpMacaco);

        success.Should().BeTrue();
        message.Should().Contain("nível 10/10");
        message.Should().Contain("**Evoluiu para 👑 Macaco-Alfa**");
        message.Should().Contain("Bônus dobrado (+4%/nível)");
    }

    [Fact]
    public async Task GetUpgradePercentByMainIdAsync_PetEvoluido_DobraBonus()
    {
        _catalog = new List<ShopItem> { EvolvedMacaco };
        _inventory.Add(Owned(EvolvedMacaco, 10));
        var shop = CreateService();

        var bonus = await shop.GetUpgradePercentByMainIdAsync(1, UpgradeEffect.Daily);

        bonus.Should().Be(40);
    }

    [Fact]
    public async Task GetUpgradePercentByMainIdAsync_PetNaoEvoluido_ValorOriginal()
    {
        _catalog = new List<ShopItem> { EvolvedMacaco };
        _inventory.Add(Owned(EvolvedMacaco, 9));
        var shop = CreateService();

        var bonus = await shop.GetUpgradePercentByMainIdAsync(1, UpgradeEffect.Daily);

        bonus.Should().Be(18);
    }

    [Fact]
    public async Task GetUpgradePercentByMainIdAsync_AplicaEfeitosNovos()
    {
        _catalog = new List<ShopItem> { Pet2D, PetRussel, PetChaves };
        _inventory.Add(Owned(Pet2D, 4));
        _inventory.Add(Owned(PetRussel, 5));
        _inventory.Add(Owned(PetChaves, 3));
        var shop = CreateService();

        var casino = await shop.GetUpgradePercentByMainIdAsync(1, UpgradeEffect.Casino);
        var robDefense = await shop.GetUpgradePercentByMainIdAsync(1, UpgradeEffect.RobDefense);
        var savings = await shop.GetUpgradePercentByMainIdAsync(1, UpgradeEffect.Savings);

        casino.Should().Be(8);
        robDefense.Should().Be(15);
        savings.Should().Be(6);
    }

    [Fact]
    public async Task GetUpgradePercentByMainIdsAsync_EmLote_SomaPorUsuario()
    {
        _catalog = new List<ShopItem> { Pet2D, PetChaves };
        _inventory.Add(Owned(Pet2D, 4));
        _inventory.Add(Owned(PetChaves, 2));
        _shopRepo.GetPetLevelsAsync(
            Arg.Any<IEnumerable<ulong>>(),
            Arg.Any<IEnumerable<string>>()).Returns(_ => _inventory);
        var shop = CreateService();

        var result = await shop.GetUpgradePercentByMainIdsAsync(new[] { 1UL }, UpgradeEffect.Casino);

        result.Should().ContainKey(1UL);
        result[1UL].Should().Be(8);
    }

    [Fact]
    public async Task RenamePetAsync_RenomeiaEPersiste()
    {
        _catalog = new List<ShopItem> { Macaco };
        _inventory.Add(Owned(Macaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(Macaco, 1));
        var shop = CreateService();

        var (success, message) = await shop.RenamePetAsync(2, "pet_macaco", "Babuíno");

        success.Should().BeTrue();
        message.Should().Contain("Babuíno");
        await _shopRepo.Received(1).SetPetNicknameAsync(1, "pet_macaco", "Babuíno");
    }

    [Fact]
    public async Task RenamePetAsync_ApelidoVazioLimpa()
    {
        _catalog = new List<ShopItem> { Macaco };
        _inventory.Add(Owned(Macaco, 1));
        _shopRepo.GetInventoryByKeyAsync(1, "pet_macaco").Returns(Owned(Macaco, 1));
        var shop = CreateService();

        var (success, message) = await shop.RenamePetAsync(2, "pet_macaco", null);

        success.Should().BeTrue();
        message.Should().Contain("voltou a se chamar");
        await _shopRepo.Received(1).SetPetNicknameAsync(1, "pet_macaco", null);
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