using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class InflationRulesTests
{
    [Fact]
    public void Index_SempreRetornaUm()
        => InflationRules.Index(0).Should().Be(1.0);

    [Theory]
    [InlineData(0UL)]
    [InlineData(125_000UL)]
    [InlineData(250_000UL)]
    [InlineData(1_000_000UL)]
    public void Index_RetornaUmParaQualquerSupply(ulong supply)
        => InflationRules.Index(supply).Should().Be(1.0);

    [Fact]
    public void Inflate_RetornaBaseValueSemModificacao()
    {
        InflationRules.Inflate(1000, 0).Should().Be(1000);
        InflationRules.Inflate(1000, 250_000).Should().Be(1000);
        InflationRules.Inflate(100, 1_000_000).Should().Be(100);
        InflationRules.Inflate(0, 999_999).Should().Be(0);
    }
}
