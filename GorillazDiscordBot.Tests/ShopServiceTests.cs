using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopServiceTests
{
    private readonly IShopRepository _shop = Substitute.For<IShopRepository>();
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();

    public ShopServiceTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
    }

    [Fact]
    public async Task BuyAsync_DescontaEAdicionaAoInventario()
    {
        var item = MakeItem("dailyx2", "Luvas de Ouro", 3000, ItemCategory.Boost, BoostEffect.DailyX2);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 5000 });
        _economy.TryDeductMoneyAsync(1, 3000, EconomyTransactionType.Purchase, Arg.Any<string>())
            .Returns((true, 2000UL));
        var service = CreateService();

        var (success, message, balance) = await service.BuyAsync(2, "alt", item);

        success.Should().BeTrue();
        message.Should().BeNull();
        balance.Should().Be(2000);
        await _shop.Received(1).AddOrIncrementInventoryAsync(Arg.Is<InventoryItem>(i =>
            i.UserId == 1 && i.ItemKey == "dailyx2" && i.Quantity == 1));
    }

    [Fact]
    public async Task BuyAsync_SemSaldo_FalhaENaoAdicionaInventario()
    {
        var item = MakeItem("trofeu", "Troféu Prime", 5000, ItemCategory.Cosmetic, BoostEffect.None);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        _economy.TryDeductMoneyAsync(1, 5000, EconomyTransactionType.Purchase, Arg.Any<string>())
            .Returns((false, 0UL));
        var service = CreateService();

        var (success, message, _) = await service.BuyAsync(2, "alt", item);

        success.Should().BeFalse();
        message.Should().Contain("moedas suficientes");
        await _shop.DidNotReceive().AddOrIncrementInventoryAsync(Arg.Any<InventoryItem>());
    }

    [Fact]
    public async Task SellAsync_ReembolsaMetadeEDecrementa()
    {
        var item = MakeItem("camisa", "Camisa 2D", 7500, ItemCategory.Cosmetic, BoostEffect.None);
        _shop.GetInventoryByKeyAsync(1, "camisa").Returns(new InventoryItem { UserId = 1, ItemKey = "camisa", Quantity = 2 });
        _economy.AddMoneyAsync(1, (ulong)Math.Floor(7500 * 0.5), EconomyTransactionType.Sell, Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 4000 });
        var service = CreateService();

        var (success, _, balance) = await service.SellAsync(2, "alt", item);

        success.Should().BeTrue();
        balance.Should().Be(4000);
        await _economy.Received(1).AddMoneyAsync(1, 3750, EconomyTransactionType.Sell, Arg.Any<string>());
        await _shop.Received(1).DecrementOrRemoveInventoryAsync(1, "camisa");
    }

    [Fact]
    public async Task SellAsync_SemItem_Falha()
    {
        var item = MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None);
        _shop.GetInventoryByKeyAsync(1, "banana").Returns((InventoryItem?)null);
        var service = CreateService();

        var (success, message, _) = await service.SellAsync(2, "alt", item);

        success.Should().BeFalse();
        message.Should().Contain("não possui");
        await _economy.DidNotReceive().AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UseAsync_BoostDailyX2_SetaFlagEConsome()
    {
        var item = MakeItem("dailyx2", "Luvas de Ouro", 3000, ItemCategory.Boost, BoostEffect.DailyX2);
        _shop.GetInventoryByKeyAsync(1, "dailyx2").Returns(new InventoryItem { UserId = 1, ItemKey = "dailyx2", Quantity = 1 });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        var (success, _, _) = await service.UseAsync(2, "alt", item);

        success.Should().BeTrue();
        await _economy.Received(1).SetDailyBoostAsync(1, true);
        await _economy.DidNotReceive().SetWorkBoostAsync(Arg.Any<ulong>(), Arg.Any<bool>());
        await _shop.Received(1).DecrementOrRemoveInventoryAsync(1, "dailyx2");
    }

    [Fact]
    public async Task UseAsync_BoostRobShield_DefineValidade()
    {
        var item = MakeItem("escudo", "Escudo Anti-Roubo", 2500, ItemCategory.Boost, BoostEffect.RobShield, 24);
        _shop.GetInventoryByKeyAsync(1, "escudo").Returns(new InventoryItem { UserId = 1, ItemKey = "escudo", Quantity = 1 });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        var (success, _, _) = await service.UseAsync(2, "alt", item);

        success.Should().BeTrue();
        await _economy.Received(1).SetRobShieldAsync(1, Arg.Is<DateTime?>(d =>
            d.HasValue && d.Value > DateTime.UtcNow.AddHours(23)));
    }

    [Fact]
    public async Task UseAsync_Cosmetico_Falha()
    {
        var item = MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None);
        var service = CreateService();

        var (success, message, _) = await service.UseAsync(2, "alt", item);

        success.Should().BeFalse();
        message.Should().Contain("cosmético");
    }

    [Fact]
    public async Task FindItemAsync_SemCache_BuscaNoBanco()
    {
        _shop.GetAllAsync().Returns(new List<ShopItem> { MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None) });
        var service = CreateService();

        var found = await service.FindItemAsync("banana");

        found.Should().NotBeNull();
        found!.Key.Should().Be("banana");
        await _shop.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task GetCatalogAsync_USaCacheDeDezMinutos()
    {
        _shop.GetAllAsync().Returns(new List<ShopItem> { MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None) });
        var service = CreateService();

        await service.GetCatalogAsync();
        await service.GetCatalogAsync();

        await _shop.Received(1).GetAllAsync();
    }

    [Fact]
    public async Task ForceReload_RecarregaDoBanco()
    {
        _shop.GetAllAsync().Returns(new List<ShopItem> { MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None) });
        var service = CreateService();

        await service.GetCatalogAsync();
        service.ForceReload();
        await service.GetCatalogAsync();

        await _shop.Received(2).GetAllAsync();
    }

    [Fact]
    public async Task BuyAsync_AssetAcimaDoLimite_Bloqueia()
    {
        var item = MakeAsset("fazenda", "Fazenda Gorillaz", 60000, 6000);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 500000 });
        _shop.GetInventoryByKeyAsync(1, "fazenda").Returns(new InventoryItem { UserId = 1, ItemKey = "fazenda", Quantity = 1 });
        var service = CreateService();

        var (success, message, _) = await service.BuyAsync(2, "alt", item);

        success.Should().BeFalse();
        message.Should().Contain("limite");
        await _economy.DidNotReceive().TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task BuyAsync_AssetLegal_DefineTimestamps()
    {
        var item = MakeAsset("terreno", "Terreno da Ilha", 150000, 15000);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 500000 });
        _shop.GetInventoryByKeyAsync(1, "terreno").Returns((InventoryItem?)null);
        _economy.TryDeductMoneyAsync(1, 150000, EconomyTransactionType.Purchase, Arg.Any<string>())
            .Returns((true, 350000UL));
        var service = CreateService();

        var (success, _, _) = await service.BuyAsync(2, "alt", item);

        success.Should().BeTrue();
        await _shop.Received(1).AddOrIncrementInventoryAsync(Arg.Is<InventoryItem>(i =>
            i.AcquiredAt.HasValue && i.LastCollectedAt.HasValue));
    }

    [Fact]
    public async Task ApplyAssetIncomesAsync_PagaDiasDesdeUltimaColeta()
    {
        var acoes = MakeAsset("acoes", "Ações da Fazenda", 20000, 2000);
        _shop.GetAllAsync().Returns(new List<ShopItem> { acoes });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 0 });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "acoes", Quantity = 1,
                LastCollectedAt = DateTime.UtcNow.AddDays(-2) }
        });
        _economy.AddMoneyAsync(1, Arg.Any<ulong>(), EconomyTransactionType.Income, Arg.Any<string>()).Returns(true);
        var service = CreateService();

        var (total, days, items) = await service.ApplyAssetIncomesAsync(2, "alt");

        total.Should().Be(4000);
        days.Should().Be(2);
        items.Should().Be(1);
        await _economy.Received(1).AddMoneyAsync(1, 4000, EconomyTransactionType.Income, Arg.Any<string>());
        await _shop.Received(1).UpdateIncomeTimestampAsync(1, "acoes", Arg.Any<DateTime>());
    }

    [Fact]
    public async Task ApplyAssetIncomesAsync_RespeitaBacklogMaximo()
    {
        var acoes = MakeAsset("acoes", "Ações da Fazenda", 20000, 2000);
        _shop.GetAllAsync().Returns(new List<ShopItem> { acoes });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 0 });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "acoes", Quantity = 1,
                LastCollectedAt = DateTime.UtcNow.AddDays(-10) }
        });
        _economy.AddMoneyAsync(1, Arg.Any<ulong>(), EconomyTransactionType.Income, Arg.Any<string>()).Returns(true);
        var service = CreateService();

        var (total, days, _) = await service.ApplyAssetIncomesAsync(2, "alt");

        total.Should().Be(6000); // 3 dias máx x 2000
        days.Should().Be(3);
    }

    [Fact]
    public async Task ApplyAssetIncomesAsync_SemDias_NaoPaga()
    {
        var acoes = MakeAsset("acoes", "Ações da Fazenda", 20000, 2000);
        _shop.GetAllAsync().Returns(new List<ShopItem> { acoes });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 0 });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "acoes", Quantity = 1,
                LastCollectedAt = DateTime.UtcNow }
        });
        var service = CreateService();

        var (total, days, items) = await service.ApplyAssetIncomesAsync(2, "alt");

        total.Should().Be(0);
        days.Should().Be(0);
        items.Should().Be(0);
        await _economy.DidNotReceive().AddMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SeedIfEmptyAsync_AdicionaFaltantesSemSobrescreverExistentes()
    {
        var existing = MakeItem("banana", "Banana Dourada com preço editado", 9999, ItemCategory.Cosmetic, BoostEffect.None);
        _shop.GetAllAsync().Returns(new List<ShopItem> { existing });
        var service = CreateService();

        await service.SeedIfEmptyAsync();

        await _shop.DidNotReceive().UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "banana"));
        await _shop.Received(1).UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "acoes"));
        await _shop.Received(1).UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "empresa"));
        await _shop.Received(1).UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "segredo"));
        await _shop.Received(1).UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "bau"));
        await _shop.Received(1).UpsertAsync(Arg.Is<ShopItem>(i => i.Key == "evento"));
    }

    [Fact]
    public async Task BuyAsync_Placeholder_Bloqueia()
    {
        var item = MakePlaceholder("segredo", "??? Ídolo Secreto");
        var service = CreateService();

        var (success, message, _) = await service.BuyAsync(2, "alt", item);

        success.Should().BeFalse();
        message.Should().Contain("ainda não está disponível");
        await _economy.DidNotReceive().TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ApplyAssetIncomesAsync_PlaceholdersNaoSaoContabilizados()
    {
        var acoes = MakeAsset("acoes", "Ações da Fazenda", 20000, 2000);
        var segredo = MakePlaceholder("segredo", "??? Ídolo Secreto");
        _shop.GetAllAsync().Returns(new List<ShopItem> { acoes, segredo });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 0 });
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "acoes", Quantity = 1, LastCollectedAt = DateTime.UtcNow.AddDays(-1) },
            new() { UserId = 1, ItemKey = "segredo", Quantity = 1, LastCollectedAt = DateTime.UtcNow.AddDays(-1) }
        });
        _economy.AddMoneyAsync(1, Arg.Any<ulong>(), EconomyTransactionType.Income, Arg.Any<string>()).Returns(true);
        var service = CreateService();

        var (total, days, items) = await service.ApplyAssetIncomesAsync(2, "alt");

        items.Should().Be(1);
        total.Should().Be(2000);
    }

    [Fact]
    public async Task EquipAsync_EquipaEDesequipaOutro()
    {
        var relogio = MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.All, 10);
        _shop.GetAllAsync().Returns(new List<ShopItem> { relogio });
        _shop.GetInventoryByKeyAsync(1, "relogio").Returns(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1 });
        var service = CreateService();

        var (success, message) = await service.EquipAsync(2, "alt", "relogio");

        success.Should().BeTrue();
        message.Should().Contain("equipado");
        await _shop.Received(1).UnequipAllAsync(1);
        await _shop.Received(1).SetEquippedAsync(1, "relogio", true);
    }

    [Fact]
    public async Task EquipAsync_ItemNaoRelic_Falha()
    {
        var banana = MakeItem("banana", "Banana Dourada", 1000, ItemCategory.Cosmetic, BoostEffect.None);
        _shop.GetAllAsync().Returns(new List<ShopItem> { banana });
        _shop.GetInventoryByKeyAsync(1, "banana").Returns(new InventoryItem { UserId = 1, ItemKey = "banana", Quantity = 1 });
        var service = CreateService();

        var (success, message) = await service.EquipAsync(2, "alt", "banana");

        success.Should().BeFalse();
        message.Should().Contain("não é um relógio");
        await _shop.DidNotReceive().SetEquippedAsync(Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task EquipAsync_SemItem_Falha()
    {
        var relogio = MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.All, 10);
        _shop.GetAllAsync().Returns(new List<ShopItem> { relogio });
        _shop.GetInventoryByKeyAsync(1, "relogio").Returns((InventoryItem?)null);
        var service = CreateService();

        var (success, message) = await service.EquipAsync(2, "alt", "relogio");

        success.Should().BeFalse();
        message.Should().Contain("não possui");
    }

    [Fact]
    public async Task UnequipAsync_SoQuandoAtivo()
    {
        var relogio = MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.All, 10);
        _shop.GetAllAsync().Returns(new List<ShopItem> { relogio });
        _shop.GetInventoryByKeyAsync(1, "relogio").Returns(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        var service = CreateService();

        var (success, _) = await service.UnequipAsync(2, "relogio");

        success.Should().BeTrue();
        await _shop.Received(1).SetEquippedAsync(1, "relogio", false);
    }

    [Fact]
    public async Task UnequipAsync_PorNome_ResolveECallama()
    {
        var relogio = MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.All, 10);
        relogio.Name = "Relógio do Cassino";
        _shop.GetAllAsync().Returns(new List<ShopItem> { relogio });
        _shop.GetInventoryByKeyAsync(1, "relogio").Returns(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        var service = CreateService();

        var (success, message) = await service.UnequipAsync(2, "Relógio do Cassino");

        success.Should().BeTrue();
        message.Should().Contain("Relógio do Cassino");
        await _shop.Received(1).SetEquippedAsync(1, "relogio", false);
    }

    [Fact]
    public async Task GetEquippedRelicAsync_SemEquipado_RetornaNulo()
    {
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>());
        var service = CreateService();

        var (relic, value, cashback) = await service.GetEquippedRelicAsync(2);

        relic.Should().BeNull();
        value.Should().Be(0);
        cashback.Should().BeFalse();
    }

    [Fact]
    public async Task GetEquippedRelicAsync_RetornaRelicEquipado()
    {
        var relogio = MakeRelic("relogio_cash", RelicEffect.Cashback, RelicGameType.All, 15);
        _shop.GetInventoryAsync(1).Returns(new List<InventoryItem>
        {
            new() { UserId = 1, ItemKey = "relogio_cash", Quantity = 1, IsEquipped = true }
        });
        _shop.GetAllAsync().Returns(new List<ShopItem> { relogio });
        var service = CreateService();

        var (relic, value, cashback) = await service.GetEquippedRelicAsync(2);

        relic.Should().NotBeNull();
        value.Should().Be(15);
        cashback.Should().BeTrue();
    }

    [Fact]
    public async Task UseAsync_BoostRobShield_ExtendeQuandoJaAtivo()
    {
        var item = MakeItem("escudo", "Escudo Anti-Roubo", 2500, ItemCategory.Boost, BoostEffect.RobShield, 24);
        _shop.GetInventoryByKeyAsync(1, "escudo").Returns(new InventoryItem { UserId = 1, ItemKey = "escudo", Quantity = 1 });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile
        {
            UserId = 1,
            RobShieldUntil = DateTime.UtcNow.AddHours(10)
        });
        var service = CreateService();

        var (success, _, _) = await service.UseAsync(2, "alt", item);

        success.Should().BeTrue();
        await _economy.Received(1).SetRobShieldAsync(1, Arg.Is<DateTime?>(d =>
            d.HasValue && d.Value > DateTime.UtcNow.AddHours(33)));
    }

    [Fact]
    public async Task UseAsync_RegistraTransacaoBoost()
    {
        var item = MakeItem("dailyx2", "Luvas de Ouro", 3000, ItemCategory.Boost, BoostEffect.DailyX2);
        _shop.GetInventoryByKeyAsync(1, "dailyx2").Returns(new InventoryItem { UserId = 1, ItemKey = "dailyx2", Quantity = 1 });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        await service.UseAsync(2, "alt", item);

        await _economy.Received(1).LogTransactionAsync(1, EconomyTransactionType.BoostUse, 0, Arg.Any<string>());
    }

    [Fact]
    public async Task UseAsync_BoostDailyX2_DefineExpiracao()
    {
        var item = MakeItem("dailyx2", "Luvas de Ouro", 3000, ItemCategory.Boost, BoostEffect.DailyX2);
        _shop.GetInventoryByKeyAsync(1, "dailyx2").Returns(new InventoryItem { UserId = 1, ItemKey = "dailyx2", Quantity = 1 });
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        var (success, _, _) = await service.UseAsync(2, "alt", item);

        success.Should().BeTrue();
        await _economy.Received(1).SetDailyBoostAsync(1, true);
    }

    private static ShopItem MakeRelic(string key, RelicEffect effect, RelicGameType game, int value)
        => new()
        {
            Key = key,
            Name = key,
            Emoji = "⌚",
            Description = "relogio",
            Price = 10000,
            Category = ItemCategory.Relic,
            Effect = BoostEffect.None,
            IsActive = true,
            RelicEffect = effect,
            RelicGame = game,
            RelicValue = value
        };

    private static ShopItem MakePlaceholder(string key, string name)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = "🔮",
            Description = "placeholder",
            Price = 0,
            Category = ItemCategory.Cosmetic,
            Effect = BoostEffect.None,
            IsActive = true,
            IsPlaceholder = true
        };

    private static ShopItem MakeAsset(string key, string name, ulong price, ulong dailyIncome)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = "🏢",
            Description = "ativo",
            Price = price,
            Category = ItemCategory.Asset,
            Effect = BoostEffect.None,
            DailyIncome = dailyIncome,
            MaxQuantity = 1,
            IsActive = true
        };

    private static ShopItem MakeItem(
        string key, string name, ulong price, ItemCategory category, BoostEffect effect, int durationHours = 0)
        => new()
        {
            Key = key,
            Name = name,
            Emoji = "🎁",
            Description = "desc",
            Price = price,
            Category = category,
            Effect = effect,
            DurationHours = durationHours,
            IsActive = true
        };

    private ShopService CreateService()
        => new(_shop, _economy, _accessor);
}