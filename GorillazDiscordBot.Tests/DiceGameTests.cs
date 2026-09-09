using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class DiceGameTests
{
    [Fact]
    public void Roll_RetornaDadosDeterministicos()
    {
        var game = new DiceGame(DiceBetType.High, () => 3);

        var result = game.Roll();

        result.Should().Be((3, 3));
        game.Result.Should().Be((3, 3));
        game.HasRolled.Should().BeTrue();
        game.Total.Should().Be(6);
        game.IsDoubles.Should().BeTrue();
    }

    [Fact]
    public void Roll_DuasVezes_LancaExcecao()
    {
        var game = new DiceGame(DiceBetType.High, () => 4);
        game.Roll();

        var act = () => game.Roll();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BetTypeHigh_Total8a12_Vence()
    {
        var game = new DiceGame(DiceBetType.High, () => 4);
        game.Roll();

        game.CalculateReturn(100).Should().Be(200);
    }

    [Fact]
    public void BetTypeHigh_TotalAbaixoDe8_Perde()
    {
        var game = new DiceGame(DiceBetType.High, () => 3);
        game.Roll();

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void BetTypeLow_Total2a6_Vence()
    {
        var game = new DiceGame(DiceBetType.Low, () => 3);
        game.Roll();

        game.CalculateReturn(100).Should().Be(200);
    }

    [Fact]
    public void BetTypeLow_Total7_Perde()
    {
        var game = new DiceGame(DiceBetType.Low, () => 4);
        game.Roll();

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void BetTypeSeven_SomaIgualASete_Vence()
    {
        var i = 0;
        var game = new DiceGame(DiceBetType.Seven, () => i++ % 2 == 0 ? 3 : 4);
        game.Roll();

        game.Total.Should().Be(7);
        game.CalculateReturn(100).Should().Be(500);
    }

    [Fact]
    public void BetTypeSeven_SomaDiferente_Perde()
    {
        var game = new DiceGame(DiceBetType.Seven, () => 4);
        game.Roll();

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void BetTypeDoubles_DadosIguais_Vence()
    {
        var game = new DiceGame(DiceBetType.Doubles, () => 2);
        game.Roll();

        game.CalculateReturn(100).Should().Be(600);
    }

    [Fact]
    public void BetTypeDoubles_DadosDiferentes_Perde()
    {
        var i = 0;
        var game = new DiceGame(DiceBetType.Doubles, () => i++ % 2 == 0 ? 2 : 5);
        game.Roll();

        game.IsDoubles.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(0);
    }

    [Theory]
    [InlineData(DiceBetType.High, 8, false, true)]
    [InlineData(DiceBetType.High, 7, false, false)]
    [InlineData(DiceBetType.Low, 6, false, true)]
    [InlineData(DiceBetType.Low, 7, false, false)]
    [InlineData(DiceBetType.Seven, 7, false, true)]
    [InlineData(DiceBetType.Seven, 8, false, false)]
    [InlineData(DiceBetType.Doubles, 8, true, true)]
    [InlineData(DiceBetType.Doubles, 8, false, false)]
    public void IsWin_AvaliaCondicao(DiceBetType type, int total, bool isDoubles, bool expected)
    {
        DiceGame.IsWin(type, total, isDoubles).Should().Be(expected);
    }

    [Theory]
    [InlineData(DiceBetType.High, 2)]
    [InlineData(DiceBetType.Low, 2)]
    [InlineData(DiceBetType.Seven, 5)]
    [InlineData(DiceBetType.Doubles, 6)]
    public void Multiplier_RetornaValorEsperado(DiceBetType type, int expected)
    {
        DiceGame.Multiplier(type).Should().Be(expected);
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var game = new DiceGame(DiceBetType.High);

        var act = () => game.CalculateReturn(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}