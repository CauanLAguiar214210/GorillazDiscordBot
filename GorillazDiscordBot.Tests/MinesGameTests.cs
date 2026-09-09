using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class MinesGameTests
{
    [Fact]
    public void Construtor_SelecionaMinasDoPicker()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);

        game.Mines.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
        game.MinesCount.Should().Be(4);
        game.GridSize.Should().Be(CasinoRules.MinesGridSize);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Reveal_CelulaSegura_AumentaMultiplicador()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);

        var safe = game.Reveal(4);

        safe.Should().BeTrue();
        game.RevealedCount.Should().Be(1);
        game.CurrentMultiplier.Should().BeGreaterThan(1.0);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Reveal_Mina_Detona()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);

        var safe = game.Reveal(1);

        safe.Should().BeFalse();
        game.HasBoom.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void CashOut_ResgataApostaVezesMultiplicador()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);
        game.Reveal(4);
        game.Reveal(5);

        var amount = game.CashOut();

        amount.Should().Be((ulong)Math.Floor(100 * game.CurrentMultiplier));
        game.HasCashedOut.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void CashOut_DepoisDaExplosao_LancaExcecao()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);
        game.Reveal(1);

        var act = () => game.CashOut();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reveal_CelulaJaRevelada_LancaExcecao()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);
        game.Reveal(4);

        var act = () => game.Reveal(4);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reveal_CelulaInvalida_LancaExcecao()
    {
        var pick = 0;
        var game = new MinesGame(100, 4, () => pick++);

        var act = () => game.Reveal(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new MinesGame(0, 4, () => 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void QuantidadeDeMinasInvalida_LancaExcecao()
    {
        var act = () => new MinesGame(100, 0, () => 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}