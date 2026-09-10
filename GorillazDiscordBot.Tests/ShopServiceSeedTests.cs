using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopServiceSeedTests
{
    private readonly IShopRepository _shop = Substitute.For<IShopRepository>();
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();

    [Fact]
    public async Task SeedIfEmptyAsync_PetAntigoSemUpgrade_MigraPreservandoId()
    {
        var storedPet = new ShopItem
        {
            Id = "5f9f2999c1a2b3c4d5e6f701",
            Key = "pet_macaco",
            Name = "Macaco-Caçador",
            Category = ItemCategory.Pet,
            UpgradeEffect = UpgradeEffect.None
        };

        var upserted = new List<ShopItem>();
        _shop.GetAllAsync().Returns(new List<ShopItem> { storedPet });
        _shop.GetByKeyAsync("pet_macaco").Returns(storedPet);
        _ = _shop.UpsertAsync(Arg.Do<ShopItem>(i => upserted.Add(i)));

        var service = new ShopService(_shop, _economy, _accessor);

        await service.SeedIfEmptyAsync();

        var migrated = upserted.FirstOrDefault(i => i.Key == "pet_macaco");
        migrated.Should().NotBeNull();
        migrated!.Id.Should().Be(storedPet.Id);
        migrated.UpgradeEffect.Should().Be(UpgradeEffect.Daily);
    }

    [Fact]
    public async Task SeedIfEmptyAsync_PetComUpgrade_NaoMigraNovamente()
    {
        var storedPet = new ShopItem
        {
            Id = "5f9f2999c1a2b3c4d5e6f701",
            Key = "pet_macaco",
            Name = "Macaco-Caçador",
            Category = ItemCategory.Pet,
            UpgradeEffect = UpgradeEffect.Daily
        };

        var upserted = new List<ShopItem>();
        _shop.GetAllAsync().Returns(new List<ShopItem> { storedPet });
        _shop.GetByKeyAsync("pet_macaco").Returns(storedPet);
        _ = _shop.UpsertAsync(Arg.Do<ShopItem>(i => upserted.Add(i)));

        var service = new ShopService(_shop, _economy, _accessor);

        await service.SeedIfEmptyAsync();

        upserted.Should().NotContain(i => i.Key == "pet_macaco");
    }
}