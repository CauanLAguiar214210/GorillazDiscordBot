using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class RaceGameTests
{
    [Fact]
    public void Construtor_SeisCorredores()
    {
        var game = new RaceGame(0);

        game.Runners.Should().Be(CasinoRules.RaceRunners);
        game.HasFinished.Should().BeFalse();
    }

    [Fact]
    public void Start_SeuCavaloVence_GanhaCincoVezes()
    {
        var game = new RaceGame(0, 6, () => 0);

        var winner = game.Start();

        winner.Should().Be(0);
        game.IsWin.Should().BeTrue();
        game.HasFinished.Should().BeTrue();
        game.CalculateReturn(100).Should().Be(500);
    }

    [Fact]
    public void Start_OutroCavaloVence_Perde()
    {
        var game = new RaceGame(0, 6, () => 3);

        game.Start();

        game.IsWin.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void Start_Duplicado_LancaExcecao()
    {
        var game = new RaceGame(0, 6, () => 0);
        game.Start();

        var act = () => game.Start();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CorredorInvalido_LancaExcecao()
    {
        var act = () => new RaceGame(6, 6, () => 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}