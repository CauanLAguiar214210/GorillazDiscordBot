using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class CasinoRulesTests
{
    [Fact]
    public void IsValidBet_AceitaValoresDentroDoIntervalo()
    {
        CasinoRules.IsValidBet(CasinoRules.MinBet).Should().BeTrue();
        CasinoRules.IsValidBet(CasinoRules.MaxBet).Should().BeTrue();
        CasinoRules.IsValidBet(500).Should().BeTrue();
    }

    [Fact]
    public void IsValidBet_RejeitaForaDoIntervalo()
    {
        CasinoRules.IsValidBet(0).Should().BeFalse();
        CasinoRules.IsValidBet(CasinoRules.MinBet - 1).Should().BeFalse();
        CasinoRules.IsValidBet(CasinoRules.MaxBet + 1).Should().BeFalse();
    }

    [Theory]
    [InlineData("DiceHighPayout", 2)]
    [InlineData("DiceLowPayout", 2)]
    [InlineData("DiceSevenPayout", 5)]
    [InlineData("DiceDoublesPayout", 6)]
    [InlineData("CoinFlipPayout", 2)]
    [InlineData("MinesDefaultCount", 4)]
    [InlineData("MinesGridSize", 16)]
    [InlineData("RpsPayout", 2)]
    [InlineData("RaceRunners", 6)]
    [InlineData("BaccaratTiePayout", 9)]
    public void ConstantesNovosJogos_CarregamValorEsperado(string constantName, int expected)
    {
        var value = typeof(CasinoRules).GetField(constantName)!.GetValue(null);
        value.Should().Be(expected);
    }

    [Fact]
    public void ConstantesMargemDaCasa_CarregamValorEsperado()
    {
        CasinoRules.MinesHouseEdge.Should().Be(0.95);
        CasinoRules.LimboHouseEdge.Should().Be(0.97);
        CasinoRules.WheelHouseEdge.Should().Be(0.95);
        CasinoRules.HighLowMultiplierStep.Should().Be(2.0);
        CasinoRules.BaccaratBankerPayout.Should().Be(1.95);
    }

    [Fact]
    public void ConstantesAviao_CarregamValorEsperado()
    {
        CasinoRules.AviaoStartMultiplier.Should().Be(1.0);
        CasinoRules.AviaoStepMultiplier.Should().Be(0.1);
        CasinoRules.AviaoMinCrash.Should().Be(1.0);
        CasinoRules.AviaoMaxCrash.Should().Be(5.0);
    }
}
