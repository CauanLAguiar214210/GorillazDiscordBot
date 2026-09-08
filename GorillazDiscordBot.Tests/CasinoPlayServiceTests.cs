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

    public CasinoPlayServiceTests()
    {
        _accessor.ResolveMainIdAsync(2).Returns(1UL);
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

        var balance = await service.PayOutAsync(2, 30, "alt", "premio");

        balance.Should().Be(80);
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

    private CasinoPlayService CreateService()
        => new(_economy, _accessor);
}