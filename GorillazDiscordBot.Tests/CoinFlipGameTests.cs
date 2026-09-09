using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class CoinFlipGameTests
{
    [Fact]
    public void Flip_RevelaCara_QuandoFlipVerdadeiro()
    {
        var game = new CoinFlipGame(CoinSide.Cara, () => true);

        var result = game.Flip();

        result.Should().Be(CoinSide.Cara);
        game.Result.Should().Be(CoinSide.Cara);
        game.HasFlipped.Should().BeTrue();
    }

    [Fact]
    public void Flip_RevelaCoroa_QuandoFlipFalso()
    {
        var game = new CoinFlipGame(CoinSide.Coroa, () => false);

        var result = game.Flip();

        result.Should().Be(CoinSide.Coroa);
    }

    [Fact]
    public void Flip_DuasVezes_LancaExcecao()
    {
        var game = new CoinFlipGame(CoinSide.Cara, () => true);
        game.Flip();

        var act = () => game.Flip();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CalculoRetorno_Acerto_PagaDoisPorUm()
    {
        var game = new CoinFlipGame(CoinSide.Cara, () => true);
        game.Flip();

        game.CalculateReturn(100).Should().Be(200);
    }

    [Fact]
    public void CalculoRetorno_Erro_Perde()
    {
        var game = new CoinFlipGame(CoinSide.Cara, () => false);
        game.Flip();

        game.CalculateReturn(100).Should().Be(0);
    }

    [Theory]
    [InlineData(CoinSide.Cara, CoinSide.Cara, true)]
    [InlineData(CoinSide.Cara, CoinSide.Coroa, false)]
    [InlineData(CoinSide.Coroa, CoinSide.Coroa, true)]
    [InlineData(CoinSide.Coroa, CoinSide.Cara, false)]
    public void IsWin_AvaliaLadoEscolhido(CoinSide chosen, CoinSide result, bool expected)
    {
        CoinFlipGame.IsWin(chosen, result).Should().Be(expected);
    }

    [Fact]
    public void CalculoRetorno_Estatico_AcertoPaga()
    {
        CoinFlipGame.CalculateReturn(CoinSide.Cara, CoinSide.Cara, 100).Should().Be(200);
    }

    [Fact]
    public void CalculoRetorno_Estatico_ErroZera()
    {
        CoinFlipGame.CalculateReturn(CoinSide.Cara, CoinSide.Coroa, 100).Should().Be(0);
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => CoinFlipGame.CalculateReturn(CoinSide.Cara, CoinSide.Cara, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}