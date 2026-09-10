using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class CasinoPlayServiceTests
{
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IEconomyAccessor _accessor = Substitute.For<IEconomyAccessor>();
    private readonly IShopRepository _shopRepo = Substitute.For<IShopRepository>();

    private readonly List<InventoryItem> _inventory = new();

    public CasinoPlayServiceTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
        _accessor.ResolveMainIdAsync(1).Returns(1UL);
        _shopRepo.GetInventoryAsync(1).Returns(_ => _inventory);
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>());
    }

    [Fact]
    public async Task GetBalanceAsync_ResolveAltParaMain()
    {
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 50 });
        var service = CreateService();

        var balance = await service.GetBalanceAsync(2, "alt");

        balance.Should().Be(50);
        await _economy.Received(1).GetOrCreateAsync(1, "alt");
        await _economy.DidNotReceive().GetOrCreateAsync(2, Arg.Any<string>());
    }

    [Fact]
    public async Task DeductBetAsync_OperaNoMain()
    {
        _economy.TryDeductMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns((true, 40UL));
        var service = CreateService();

        var (success, balance) = await service.DeductBetAsync(2, 10, "alt", "aposta");

        success.Should().BeTrue();
        balance.Should().Be(40);
        await _economy.Received(1).GetOrCreateAsync(1, "alt");
        await _economy.Received(1).TryDeductMoneyAsync(1, 10, EconomyTransactionType.Bet, "aposta");
        await _economy.DidNotReceive().TryDeductMoneyAsync(2, Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task PayOutAsync_AdicionaNoMain()
    {
        _economy.AddMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 80 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 30, "alt", "premio");

        payout.Balance.Should().Be(80);
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
        await _economy.Received(1).AddMoneyAsync(1, 30, EconomyTransactionType.Bet, "premio");
        await _economy.DidNotReceive().AddMoneyAsync(2, Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task PayOutAsync_RetornoZero_NaoAdiciona()
    {
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        await service.PayOutAsync(2, 0, "alt", "premio");

        await _economy.DidNotReceive().AddMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
    }

    [Fact]
    public async Task PayOutAsync_RetornoZero_SemBonusNemRelic()
    {
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio");

        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_AplicaQuandoJogoBate()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.Roulette, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "premio", RelicGameType.Roulette, 50);

        await _economy.Received(1).AddMoneyAsync(1, 120, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(20);
        payout.Relic.Should().NotBeNull();
        payout.Relic!.Name.Should().Be("relogio");
        payout.Relic!.Effect.Should().Be(RelicEffect.GainBonus);
        payout.Relic!.Value.Should().Be(20);
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_NaoAplicaQuandoJogoDiferente()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.Slots, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "premio", RelicGameType.Roulette, 50);

        await _economy.Received(1).AddMoneyAsync(1, 100, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_NaoAplicaEmEmpate_RetornoIgualAposta()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.Rps, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "empate do jokenpô", RelicGameType.Rps, 100);

        await _economy.Received(1).AddMoneyAsync(1, 100, EconomyTransactionType.Bet, "empate do jokenpô");
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_NaoAplicaEmRefundoParcial()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.Wheel, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 40, "alt", "faixa da roda", RelicGameType.Wheel, 100);

        await _economy.Received(1).AddMoneyAsync(1, 40, EconomyTransactionType.Bet, "faixa da roda");
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_NaoAplicaEmPushDeBlackjack()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.Blackjack, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "push do blackjack", RelicGameType.Blackjack, 100);

        await _economy.Received(1).AddMoneyAsync(1, 100, EconomyTransactionType.Bet, "push do blackjack");
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioCashback_DevolvePercentualNaDerrota()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio_cash", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio_cash", RelicEffect.Cashback, RelicGameType.All, 15)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio", RelicGameType.Slots, 100);

        await _economy.Received(1).AddMoneyAsync(1, 15, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(15);
        payout.Relic.Should().NotBeNull();
        payout.Relic!.Name.Should().Be("relogio_cash");
        payout.Relic!.Effect.Should().Be(RelicEffect.Cashback);
        payout.Relic!.Value.Should().Be(15);
    }

    [Fact]
    public async Task PayOutAsync_SemRelogioEquipado_NaoAplicaBonus()
    {
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio", RelicGameType.All, 100);

        await _economy.DidNotReceive().AddMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioCashback_NaoAplicaQuandoJogoDiferente()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio_cash_slot", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio_cash_slot", RelicEffect.Cashback, RelicGameType.Slots, 15)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio", RelicGameType.Roulette, 100);

        await _economy.DidNotReceive().AddMoneyAsync(
            Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>());
        payout.Bonus.Should().Be(0);
        payout.Relic.Should().BeNull();
    }

    [Fact]
    public async Task PayOutAsync_RelogioGainBonus_AplicaNoBlackjack()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, RelicGameType.All, 10)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "premio", RelicGameType.Blackjack, 50);

        await _economy.Received(1).AddMoneyAsync(1, 110, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(10);
        payout.Relic.Should().NotBeNull();
        payout.Relic!.Effect.Should().Be(RelicEffect.GainBonus);
    }

    [Fact]
    public async Task PayOutAsync_RelogioCashback_DevolveNoBlackjack()
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio_cash", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio_cash", RelicEffect.Cashback, RelicGameType.All, 15)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio", RelicGameType.Blackjack, 100);

        await _economy.Received(1).AddMoneyAsync(1, 15, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(15);
        payout.Relic.Should().NotBeNull();
        payout.Relic!.Effect.Should().Be(RelicEffect.Cashback);
    }

    [Theory]
    [InlineData(RelicGameType.Dice)]
    [InlineData(RelicGameType.Coin)]
    [InlineData(RelicGameType.Aviao)]
    [InlineData(RelicGameType.VideoPoker)]
    [InlineData(RelicGameType.Mines)]
    [InlineData(RelicGameType.Limbo)]
    [InlineData(RelicGameType.Rps)]
    [InlineData(RelicGameType.Race)]
    [InlineData(RelicGameType.Plinko)]
    [InlineData(RelicGameType.Wheel)]
    [InlineData(RelicGameType.HighLow)]
    [InlineData(RelicGameType.Baccarat)]
    public async Task PayOutAsync_NovoJogo_RelogioGainBonus_AplicaQuandoJogoBate(RelicGameType game)
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio", RelicEffect.GainBonus, game, 20)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 100, "alt", "premio", game, 50);

        await _economy.Received(1).AddMoneyAsync(1, 120, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(20);
        payout.Relic.Should().NotBeNull();
    }

    [Theory]
    [InlineData(RelicGameType.Dice)]
    [InlineData(RelicGameType.Coin)]
    [InlineData(RelicGameType.Aviao)]
    [InlineData(RelicGameType.VideoPoker)]
    [InlineData(RelicGameType.Mines)]
    [InlineData(RelicGameType.Limbo)]
    [InlineData(RelicGameType.Rps)]
    [InlineData(RelicGameType.Race)]
    [InlineData(RelicGameType.Plinko)]
    [InlineData(RelicGameType.Wheel)]
    [InlineData(RelicGameType.HighLow)]
    [InlineData(RelicGameType.Baccarat)]
    public async Task PayOutAsync_NovoJogo_RelogioCashback_DevolveNaDerrota(RelicGameType game)
    {
        _inventory.Add(new InventoryItem { UserId = 1, ItemKey = "relogio_cash", Quantity = 1, IsEquipped = true });
        _shopRepo.GetAllAsync().Returns(new List<ShopItem>
        {
            MakeRelic("relogio_cash", RelicEffect.Cashback, game, 15)
        });
        _economy.AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<EconomyTransactionType>(), Arg.Any<string>())
            .Returns(true);
        _economy.GetOrCreateAsync(1, "alt").Returns(new EconomyProfile { UserId = 1, Money = 100 });
        var service = CreateService();

        var payout = await service.PayOutAsync(2, 0, "alt", "premio", game, 100);

        await _economy.Received(1).AddMoneyAsync(1, 15, EconomyTransactionType.Bet, "premio");
        payout.Bonus.Should().Be(15);
        payout.Relic.Should().NotBeNull();
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

    private CasinoPlayService CreateService()
        => new(_economy, _accessor, new ShopService(_shopRepo, _economy, _accessor));
}