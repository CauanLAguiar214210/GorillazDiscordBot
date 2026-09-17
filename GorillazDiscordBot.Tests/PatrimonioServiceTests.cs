using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class PatrimonioServiceTests
{
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IShopRepository _shop = Substitute.For<IShopRepository>();
    private readonly IRankingRepository _ranking = Substitute.For<IRankingRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();

    public PatrimonioServiceTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
        _accessor.ResolveMainIdAsync(1).Returns(1UL);
    }

    [Fact]
    public async Task GetSnapshotAsync_SomaCarteiraBancoPoupancaEItens()
    {
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile
        {
            UserId = 1,
            Money = 100,
            Bank = 50,
            Savings = 25
        });
        _shop.GetAllAsync().Returns(new List<ShopItem>
        {
            new() { Key = "camisa", Name = "Camisa 2D", Price = 7500, Category = ItemCategory.Cosmetic, IsActive = true }
        });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "camisa", Quantity = 2 }
        });
        var service = CreateService();

        var snapshot = await service.GetSnapshotAsync(2, "alt");

        snapshot.Money.Should().Be(100);
        snapshot.Bank.Should().Be(50);
        snapshot.Savings.Should().Be(25);
        snapshot.CashTotal.Should().Be(175);
        snapshot.ItemsValue.Should().Be(15000);
        snapshot.Total.Should().Be(15175);
    }

    [Fact]
    public async Task GetSnapshotAsync_ItensInexistentesVaemZero()
    {
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 10 });
        _shop.GetAllAsync().Returns(new List<ShopItem>());
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>());
        var service = CreateService();

        var snapshot = await service.GetSnapshotAsync(2, "alt");

        snapshot.ItemsValue.Should().Be(0);
        snapshot.Total.Should().Be(10);
    }

    [Fact]
    public async Task GetHallOfFameAsync_BuscaNoGrupoVinculado()
    {
        _users.GetGroupAsync(1).Returns(new List<DiscordUserProfile>
        {
            new() { UserId = 1 },
            new() { UserId = 2 }
        });
        var fame = new GorillazDiscordBot.Domain.Entity.Ranking.HallOfFame { Title = "Lenda", UserId = 2 };
        _ranking.GetHallOfFameForMembersAsync(Arg.Is<IReadOnlyList<ulong>>(ids => ids.Contains(1UL) && ids.Contains(2UL)))
            .Returns(fame);
        var service = CreateService();

        var result = await service.GetHallOfFameAsync(1);

        result.Should().BeSameAs(fame);
    }

    [Fact]
    public async Task GetTopSnapshotsAsync_OrdenaPorPatrimonioTotal()
    {
        var pobretao = new EconomyProfile { UserId = 1, Money = 1000, Username = "pobretao" };
        var ricao = new EconomyProfile { UserId = 2, Money = 500, Username = "ricao" };
        _accessor.ResolveMainIdAsync(2).Returns(2UL);
        _economy.GetTopUsersAsync(Arg.Any<int>()).Returns(new List<EconomyProfile> { pobretao, ricao });

        _shop.GetAllAsync().Returns(new List<ShopItem>
        {
            new() { Key = "trofeu", Name = "Troféu", Price = 10_000, Category = ItemCategory.Cosmetic, IsActive = true }
        });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "trofeu", Quantity = 1 }
        });
        _shop.GetInventoryAsync(2).Returns(new List<InventoryItem>());
        var service = CreateService();

        var top = await service.GetTopSnapshotsAsync(10);

        top.Should().HaveCount(2);
        top[0].UserId.Should().Be(1);
        top[0].Snapshot.Total.Should().Be(11_000);
        top[0].Snapshot.ItemsValue.Should().Be(10_000);
        top[1].UserId.Should().Be(2);
        top[1].Snapshot.Total.Should().Be(500);
    }

    private PatrimonioService CreateService()
        => new(_economy, new ShopService(_shop, _economy, _accessor, Substitute.For<ICharacterProfileRepository>()), _ranking, _accessor, _users);
}