using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class PlinkoGameTests
{
    [Fact]
    public void Drop_Centro_RetornaMeiaApostaComMultiplicadorMeio()
    {
        var game = new PlinkoGame(() => 4);

        var bin = game.Drop();

        bin.Should().Be(4);
        game.ResultBin.Should().Be(4);
        game.HasDropped.Should().BeTrue();
        PlinkoGame.Multiplier(4).Should().Be(0.5);
        game.CalculateReturn(100).Should().Be(50);
    }

    [Fact]
    public void Drop_FaixaExterna_RetornaTresVezes()
    {
        var game = new PlinkoGame(() => 0);

        game.Drop();

        PlinkoGame.Multiplier(0).Should().Be(3.0);
        game.CalculateReturn(100).Should().Be(300);
    }

    [Fact]
    public void Drop_Duplicado_LancaExcecao()
    {
        var game = new PlinkoGame(() => 2);
        game.Drop();

        var act = () => game.Drop();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CalcularRetornoSemLargar_RetornaZero()
    {
        var game = new PlinkoGame(() => 2);

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void Multiplicadores_SaoSimetricos()
    {
        PlinkoGame.Multiplier(0).Should().Be(PlinkoGame.Multiplier(8));
        PlinkoGame.Multiplier(1).Should().Be(PlinkoGame.Multiplier(7));
        PlinkoGame.Multiplier(2).Should().Be(PlinkoGame.Multiplier(6));
        PlinkoGame.Multiplier(3).Should().Be(PlinkoGame.Multiplier(5));
    }

    [Fact]
    public void MultiplicadorInvalido_LancaExcecao()
    {
        var act = () => PlinkoGame.Multiplier(9);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FaixaInvalidaNoDrop_LancaExcecao()
    {
        var game = new PlinkoGame(() => 99);

        var act = () => game.Drop();

        act.Should().Throw<InvalidOperationException>();
    }
}