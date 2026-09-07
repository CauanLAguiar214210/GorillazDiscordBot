using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class EconomyFormatTests
{
    [Theory]
    [InlineData(0UL, "0")]
    [InlineData(999UL, "999")]
    [InlineData(12_500UL, "12.500")]
    [InlineData(999_999UL, "999.999")]
    public void Compact_MenorQueMilhao_ExibeValorCompleto(ulong value, string expected)
    {
        EconomyFormat.Compact(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000_000UL, "1M")]
    [InlineData(1_500_000UL, "1,5M")]
    [InlineData(123_400_000UL, "123,4M")]
    [InlineData(999_999_999UL, "1000M")]
    public void Compact_MilhaoAoBilhao_UsaUnidadeM(ulong value, string expected)
    {
        EconomyFormat.Compact(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000_000_000UL, "1B")]
    [InlineData(2_300_000_000UL, "2,3B")]
    [InlineData(999_999_999_999UL, "1000B")]
    public void Compact_BilhaoAoTrilhao_UsaUnidadeB(ulong value, string expected)
    {
        EconomyFormat.Compact(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1_000_000_000_000UL, "1T")]
    [InlineData(1_200_000_000_000UL, "1,2T")]
    [InlineData(9_200_000_000_000UL, "9,2T")]
    public void Compact_TrilhaoOuMais_UsaUnidadeT(ulong value, string expected)
    {
        EconomyFormat.Compact(value).Should().Be(expected);
    }

    [Fact]
    public void Compact_TetosDeUlong_NaoEstoura()
    {
        var result = EconomyFormat.Compact(ulong.MaxValue);
        result.Should().EndWith("T").And.NotBeEmpty();
    }

    [Fact]
    public void Full_ExibeSeparadorDeMilhar()
    {
        EconomyFormat.Full(12_500).Should().Be("12.500");
        EconomyFormat.Full(1_500_000).Should().Be("1.500.000");
    }

    [Fact]
    public void NetWorth_SomaCarteiraBancoEPoupanca()
    {
        var profile = new EconomyProfile { Money = 100, Bank = 250, Savings = 75 };
        profile.NetWorth.Should().Be(425);
    }

    [Fact]
    public void NetWorth_Overflow_UsaTeto()
    {
        var profile = new EconomyProfile
        {
            Money = ulong.MaxValue,
            Bank = 10,
            Savings = 20
        };
        profile.NetWorth.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void NetWorth_ZeradoSemSoma()
    {
        new EconomyProfile().NetWorth.Should().Be(0);
    }
}