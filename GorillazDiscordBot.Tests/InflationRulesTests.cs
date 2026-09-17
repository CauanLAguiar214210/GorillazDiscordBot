using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class InflationRulesTests
{
    [Fact]
    public void Index_ZeroSupply_RetornaUm()
        => InflationRules.Index(0).Should().Be(1.0);

    [Theory]
    [InlineData(125_000, 1.5)]
    [InlineData(250_000, 2.0)]
    [InlineData(500_000, 3.0)]
    [InlineData(1_000_000, 5.0)]
    public void Index_Cada250kMaisCemPorcento(ulong supply, double expected)
        => InflationRules.Index(supply).Should().BeApproximately(expected, 0.0001);

    [Fact]
    public void Inflate_MultiplicaPeloIndice()
    {
        InflationRules.Inflate(1000, 0).Should().Be(1000);
        InflationRules.Inflate(1000, 250_000).Should().Be(2000);
        InflationRules.Inflate(100, 1_000_000).Should().Be(500);
    }

    [Fact]
    public void Inflate_ArredondaParaCima()
        => InflationRules.Inflate(1, 125_000).Should().Be(2);
}