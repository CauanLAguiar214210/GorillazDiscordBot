using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Tests;

public class EconomyAmountParserTests
{
    [Theory]
    [InlineData("100", 100UL)]
    [InlineData("1500", 1500UL)]
    [InlineData("1", 1UL)]
    public void TryParse_NumeroInteiroSimples(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.000", 1000UL)]
    [InlineData("1.500", 1500UL)]
    [InlineData("1.500.000", 1_500_000UL)]
    [InlineData("1.234.567.890", 1_234_567_890UL)]
    public void TryParse_SeparadorDeMilharPtBr(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1k", 1_000UL)]
    [InlineData("1K", 1_000UL)]
    [InlineData("2 mil", 2_000UL)]
    [InlineData("3mil", 3_000UL)]
    [InlineData("4 MIL", 4_000UL)]
    [InlineData("1,5k", 1_500UL)]
    public void TryParse_SufixoK_UsaMil(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1 M", 1_000_000UL)]
    [InlineData("1m", 1_000_000UL)]
    [InlineData("1 milhao", 1_000_000UL)]
    [InlineData("1 milhão", 1_000_000UL)]
    [InlineData("2 milhões", 2_000_000UL)]
    [InlineData("1,5M", 1_500_000UL)]
    [InlineData("1.5M", 1_500_000UL)]
    [InlineData("0,5M", 500_000UL)]
    public void TryParse_SufixoM_UsaMilhao(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1B", 1_000_000_000UL)]
    [InlineData("1 b", 1_000_000_000UL)]
    [InlineData("2 bilhao", 2_000_000_000UL)]
    [InlineData("2 bilhão", 2_000_000_000UL)]
    [InlineData("3 bilhões", 3_000_000_000UL)]
    [InlineData("1.5B", 1_500_000_000UL)]
    [InlineData("1,5B", 1_500_000_000UL)]
    public void TryParse_SufixoB_UsaBilhao(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1T", 1_000_000_000_000UL)]
    [InlineData("1 trilhao", 1_000_000_000_000UL)]
    [InlineData("2 trilhões", 2_000_000_000_000UL)]
    [InlineData("1,25T", 1_250_000_000_000UL)]
    public void TryParse_SufixoT_UsaTrilhao(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.500,5k", 1_500_500UL)]
    [InlineData("1.234,5M", 1_234_500_000UL)]
    public void TryParse_DotMilharComVirgulaDecimal(string input, ulong expected)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("5x")]
    [InlineData("1milion")]
    [InlineData("milharina")]
    [InlineData("20000000T")]
    [InlineData("18446744073709551616")]
    public void TryParse_Invalido(string input)
    {
        var ok = EconomyAmountParser.TryParse(input, out var amount, out var error);
        ok.Should().BeFalse();
        amount.Should().Be(0);
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryParse_TetoDeUlong_AceitaValorMaximo()
    {
        var ok = EconomyAmountParser.TryParse("18446744073709551615", out var amount, out _);
        ok.Should().BeTrue();
        amount.Should().Be(ulong.MaxValue);
    }
}