using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopServiceVehicleTests
{
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();
    private readonly IShopRepository _shopRepo = Substitute.For<IShopRepository>();
    private readonly ICharacterProfileRepository _profiles = Substitute.For<ICharacterProfileRepository>();

    private readonly List<InventoryItem> _inventory = new();
    private List<ShopItem> _catalog = new();
    private readonly CharacterProfile _profile = new();

    private static readonly ShopItem CarroPopular = new()
    {
        Key = "carro_popular",
        Name = "Carro Popular",
        Emoji = "🚗",
        Price = 40000,
        Category = ItemCategory.Vehicle,
        MaxQuantity = 1
    };

    private static readonly ShopItem Jato = new()
    {
        Key = "jato",
        Name = "Jato Executivo",
        Emoji = "🛩️",
        Price = 5000000,
        Category = ItemCategory.Vehicle,
        MaxQuantity = 1
    };

    private static readonly ShopItem Relogio = new()
    {
        Key = "relogio",
        Name = "Relógio do Cassino",
        Emoji = "⌚",
        Price = 100000,
        Category = ItemCategory.Relic,
        MaxQuantity = 1
    };

    public ShopServiceVehicleTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
        _accessor.ResolveMainIdAsync(1).Returns(1UL);
        _shopRepo.GetInventoryAsync(1).Returns(_ => _inventory);
        _shopRepo.GetInventoryByKeyAsync(1, Arg.Any<string>()).Returns(
            call => _inventory.FirstOrDefault(i => i.ItemKey == (string)call[1]));
        _profiles.GetOrCreateAsync(1, Arg.Any<string>()).Returns(_profile);
        _profiles.GetAsync(1).Returns(_profile);
        _economy.GetOrCreateAsync(1, Arg.Any<string>())
            .Returns(new EconomyProfile { UserId = 1, Money = 0 });
    }

    private ShopService CreateService()
    {
        _shopRepo.GetAllAsync().Returns(_ => _catalog);
        var shop = new ShopService(_shopRepo, _economy, _accessor, _profiles);
        shop.ForceReload();
        return shop;
    }

    private static InventoryItem Owned(ShopItem item, int qty)
        => new() { UserId = 1, ItemKey = item.Key, Quantity = qty, AcquiredAt = DateTime.UtcNow };

    [Fact]
    public async Task Drive_SemPossuir_Recusa()
    {
        _catalog = new List<ShopItem> { CarroPopular };
        var shop = CreateService();

        var (success, message) = await shop.DriveVehicleAsync(2, "alt", "carro_popular");

        success.Should().BeFalse();
        message.Should().Contain("não possui");
        _profile.VeiculoAtualKey.Should().BeNull();
    }

    [Fact]
    public async Task Drive_SemLicenca_RecusaEIndicaQualFalta()
    {
        _catalog = new List<ShopItem> { CarroPopular };
        _inventory.Add(Owned(CarroPopular, 1));
        var shop = CreateService();

        var (success, message) = await shop.DriveVehicleAsync(2, "alt", "carro_popular");

        success.Should().BeFalse();
        message.Should().Contain("licença");
        message.Should().Contain("Categoria B");
        _profile.VeiculoAtualKey.Should().BeNull();
    }

    [Fact]
    public async Task Drive_ComLicenca_EquipaESalva()
    {
        _catalog = new List<ShopItem> { CarroPopular };
        _inventory.Add(Owned(CarroPopular, 1));
        _profile.Licencas.Add(LicenseLevel.B);
        var shop = CreateService();

        var (success, message) = await shop.DriveVehicleAsync(2, "alt", "carro_popular");

        success.Should().BeTrue();
        _profile.VeiculoAtualKey.Should().Be("carro_popular");
        await _profiles.Received(1).SaveAsync(_profile);
    }

    [Fact]
    public async Task Drive_NaoEhVeiculo_Recusa()
    {
        _catalog = new List<ShopItem> { Relogio };
        _inventory.Add(Owned(Relogio, 1));
        var shop = CreateService();

        var (success, message) = await shop.DriveVehicleAsync(2, "alt", "relogio");

        success.Should().BeFalse();
        message.Should().Contain("não é um veículo");
    }

    [Fact]
    public async Task Park_SemVeiculo_Recusa()
    {
        var shop = CreateService();

        var (success, message) = await shop.ParkVehicleAsync(2);

        success.Should().BeFalse();
        message.Should().Contain("não está dirigindo");
    }

    [Fact]
    public async Task Park_ComVeiculo_LimpaVeiculoAtual()
    {
        _profile.VeiculoAtualKey = "carro_popular";
        var shop = CreateService();

        var (success, message) = await shop.ParkVehicleAsync(2);

        success.Should().BeTrue();
        _profile.VeiculoAtualKey.Should().BeNull();
        await _profiles.Received(1).SaveAsync(_profile);
    }

    [Fact]
    public async Task GetCurrentVehicle_RetornaItemDoCatalogo()
    {
        _catalog = new List<ShopItem> { CarroPopular, Jato };
        _profile.VeiculoAtualKey = "jato";
        var shop = CreateService();

        var (vehicle, key) = await shop.GetCurrentVehicleAsync(2);

        key.Should().Be("jato");
        vehicle.Should().NotBeNull();
        vehicle!.Key.Should().Be("jato");
        vehicle.Name.Should().Be("Jato Executivo");
    }

    [Fact]
    public async Task GetCurrentVehicle_SemVeiculo_RetornaNulo()
    {
        var shop = CreateService();

        var (vehicle, key) = await shop.GetCurrentVehicleAsync(2);

        key.Should().BeNull();
        vehicle.Should().BeNull();
    }

    [Fact]
    public async Task Sell_VeiculoQueEstaDirigindo_LimpaVeiculoAtual()
    {
        _catalog = new List<ShopItem> { CarroPopular };
        _inventory.Add(Owned(CarroPopular, 1));
        _profile.VeiculoAtualKey = "carro_popular";
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        var shop = CreateService();

        var (success, _, _) = await shop.SellAsync(2, "alt", CarroPopular);

        success.Should().BeTrue();
        _profile.VeiculoAtualKey.Should().BeNull();
        await _profiles.Received(1).SaveAsync(_profile);
    }
}