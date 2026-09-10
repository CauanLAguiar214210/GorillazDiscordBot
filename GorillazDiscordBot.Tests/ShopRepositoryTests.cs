using FluentAssertions;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Entity.Economy;
using MongoDB.Bson;
using MongoDB.Driver;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopRepositoryTests
{
    private readonly IMongoCollection<ShopItem> _items = Substitute.For<IMongoCollection<ShopItem>>();
    private readonly IMongoCollection<InventoryItem> _inventory = Substitute.For<IMongoCollection<InventoryItem>>();

    [Fact]
    public async Task UpsertAsync_ItemSemId_GeraObjectIdValido()
    {
        ShopItem? replaced = null;
        _items.ReplaceOneAsync(
            Arg.Any<FilterDefinition<ShopItem>>(),
            Arg.Do<ShopItem>(i => replaced = i),
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(default(ReplaceOneResult)!));

        var repo = new ShopRepository(_items, _inventory);

        await repo.UpsertAsync(new ShopItem { Key = "pet_macaco", Name = "Macaco-Caçador" });

        replaced.Should().NotBeNull();
        replaced!.Id.Should().NotBeNullOrEmpty();
        ObjectId.TryParse(replaced.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task UpsertAsync_ItemComId_ConservaId()
    {
        ShopItem? replaced = null;
        _items.ReplaceOneAsync(
            Arg.Any<FilterDefinition<ShopItem>>(),
            Arg.Do<ShopItem>(i => replaced = i),
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(default(ReplaceOneResult)!));

        var repo = new ShopRepository(_items, _inventory);

        await repo.UpsertAsync(new ShopItem { Id = "5f9f2999c1a2b3c4d5e6f701", Key = "pet_macaco" });

        replaced.Should().NotBeNull();
        replaced!.Id.Should().Be("5f9f2999c1a2b3c4d5e6f701");
    }
}