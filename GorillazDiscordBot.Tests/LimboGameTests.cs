using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class LimboGameTests
{
    [Fact]
    public void Roll_ResultadoAbaixoDoAlvo_Perde()
    {
        var game = new LimboGame(2.0, () => 0.4);

        var result = game.Roll();

        result.Should().BeApproximately(1.6667, 0.001);
        game.IsWin.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void Roll_ResultadoPassaDoAlvo_GanhaComMargem()
    {
        var game = new LimboGame(2.0, () => 0.9);

        var result = game.Roll();

        result.Should().BeApproximately(10.0, 0.001);
        game.IsWin.Should().BeTrue();
        game.CalculateReturn(100).Should().Be(194);
    }

    [Fact]
    public void Roll_ResultadoIgualAoAlvo_Ganha()
    {
        var game = new LimboGame(2.0, () => 0.5);

        game.Roll();

        game.IsWin.Should().BeTrue();
    }

    [Fact]
    public void Roll_ResultadoExcedeMil_ClampEmDezMil()
    {
        var game = new LimboGame(10.0, () => 0.9999999);

        var result = game.Roll();

        result.Should().Be(10_000);
        game.IsWin.Should().BeTrue();
    }

    [Fact]
    public void Roll_Duplicado_LancaExcecao()
    {
        var game = new LimboGame(3.0, () => 0.5);
        game.Roll();

        var act = () => game.Roll();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CalcularRetornoSemRolar_RetornaZero()
    {
        var game = new LimboGame(3.0, () => 0.5);

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void AlvoMenorQueUm_LancaExcecao()
    {
        var act = () => new LimboGame(0.9, () => 0.5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}