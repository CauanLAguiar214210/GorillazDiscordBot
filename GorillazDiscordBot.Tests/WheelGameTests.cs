using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class WheelGameTests
{
    [Fact]
    public void Spin_FixaIndiceResultado()
    {
        var game = new WheelGame(100, () => 9);

        var index = game.Spin();

        index.Should().Be(9);
        game.ResultIndex.Should().Be(9);
        game.HasSpun.Should().BeTrue();
        game.ResultMultiplier.Should().Be(5.0);
    }

    [Fact]
    public void CalcularRetorno_AplicaFatiaEMargem()
    {
        var game = new WheelGame(100, () => 9);

        game.Spin();

        game.CalculateReturn(100).Should().Be(475);
    }

    [Fact]
    public void CalcularRetorno_MeiaAposta_RetornaQuaseMetade()
    {
        var game = new WheelGame(100, () => 0);

        game.Spin();

        game.CalculateReturn(100).Should().Be(47);
    }

    [Fact]
    public void RetornoEsperado_TemCincoPorcentoDeEdge()
    {
        WheelGame.ExpectedReturn.Should().BeApproximately(0.95, 0.01);
    }

    [Fact]
    public void Spin_Duplicado_LancaExcecao()
    {
        var game = new WheelGame(100, () => 0);
        game.Spin();

        var act = () => game.Spin();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Spin_FatiaInvalida_LancaExcecao()
    {
        var game = new WheelGame(100, () => 99);

        var act = () => game.Spin();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new WheelGame(0, () => 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}