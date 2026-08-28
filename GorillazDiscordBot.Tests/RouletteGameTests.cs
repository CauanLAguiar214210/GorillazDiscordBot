using FluentAssertions;
using GorillazDiscordBot.Domain.Games;

namespace GorillazDiscordBot.Tests;

public class RouletteGameTests
{
    [Fact]
    public void Spin_RetornaNumeroNoIntervalo()
    {
        var game = new RouletteGame();

        var result = game.Spin();

        result.Should().BeInRange(0, 36);
        game.ResultNumber.Should().Be(result);
        game.HasSpun.Should().BeTrue();
    }

    [Fact]
    public void Spin_DuasVezes_LancaExcecao()
    {
        var game = new RouletteGame(() => 10);
        game.Spin();

        var act = () => game.Spin();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NumeroExato_PagaTresCincoPorUm()
    {
        var game = new RouletteGame(() => 7);
        var bet = game.AddBet(100, RouletteBetType.Number, 7);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
        game.CalculateReturn(bet).Should().Be(100 * 36);
        game.CalculateTotalReturn().Should().Be(100 * 36);
    }

    [Fact]
    public void NumeroIncorreto_Perde()
    {
        var game = new RouletteGame(() => 8);
        var bet = game.AddBet(100, RouletteBetType.Number, 7);

        game.Spin();

        game.IsWin(bet).Should().BeFalse();
        game.CalculateReturn(bet).Should().Be(0);
    }

    [Fact]
    public void CorVermelha_VenceNumeroVermelho()
    {
        var game = new RouletteGame(() => 1);
        var bet = game.AddBet(100, RouletteBetType.Color, (int)RouletteColor.Red);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
        game.CalculateReturn(bet).Should().Be(200);
    }

    [Fact]
    public void CorPreta_VenceNumeroPreto()
    {
        var game = new RouletteGame(() => 2);
        var bet = game.AddBet(100, RouletteBetType.Color, (int)RouletteColor.Black);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
        game.CalculateReturn(bet).Should().Be(200);
    }

    [Fact]
    public void Cor_NoZero_Perde()
    {
        var game = new RouletteGame(() => 0);
        var red = game.AddBet(100, RouletteBetType.Color, (int)RouletteColor.Red);
        var black = game.AddBet(100, RouletteBetType.Color, (int)RouletteColor.Black);

        game.Spin();

        game.IsWin(red).Should().BeFalse();
        game.IsWin(black).Should().BeFalse();
    }

    [Fact]
    public void ParidadePar_VenceNumeroPar()
    {
        var game = new RouletteGame(() => 12);
        var bet = game.AddBet(100, RouletteBetType.Parity, 0);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
        game.CalculateReturn(bet).Should().Be(200);
    }

    [Fact]
    public void ParidadeImpar_VenceNumeroImpar()
    {
        var game = new RouletteGame(() => 13);
        var bet = game.AddBet(100, RouletteBetType.Parity, 1);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
    }

    [Fact]
    public void Paridade_NoZero_Perde()
    {
        var game = new RouletteGame(() => 0);
        var even = game.AddBet(100, RouletteBetType.Parity, 0);

        game.Spin();

        game.IsWin(even).Should().BeFalse();
    }

    [Fact]
    public void MetadeBaixa_VenceNumeroAte18()
    {
        var game = new RouletteGame(() => 18);
        var bet = game.AddBet(100, RouletteBetType.Half, 0);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
    }

    [Fact]
    public void MetadeAlta_VenceNumeroAcimaDe18()
    {
        var game = new RouletteGame(() => 19);
        var bet = game.AddBet(100, RouletteBetType.Half, 1);

        game.Spin();

        game.IsWin(bet).Should().BeTrue();
    }

    [Fact]
    public void Metade_NoZero_Perde()
    {
        var game = new RouletteGame(() => 0);
        var low = game.AddBet(100, RouletteBetType.Half, 0);
        var high = game.AddBet(100, RouletteBetType.Half, 1);

        game.Spin();

        game.IsWin(low).Should().BeFalse();
        game.IsWin(high).Should().BeFalse();
    }

    [Fact]
    public void ApostasMultiplas_AcumulamRetorno()
    {
        var game = new RouletteGame(() => 3);
        game.AddBet(100, RouletteBetType.Number, 3);
        game.AddBet(100, RouletteBetType.Color, (int)RouletteColor.Red);
        game.AddBet(100, RouletteBetType.Parity, 1);
        game.AddBet(100, RouletteBetType.Half, 0);

        game.Spin();

        game.CalculateTotalReturn().Should().Be((100 * 36) + 200 + 200 + 200);
        game.TotalBet.Should().Be(400);
    }

    [Fact]
    public void NumeroForaDoIntervalo_LancaExcecao()
    {
        var game = new RouletteGame();

        var act = () => game.AddBet(100, RouletteBetType.Number, 37);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var game = new RouletteGame();

        var act = () => game.AddBet(0, RouletteBetType.Number, 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ColorOf_RetornaCorEsperada()
    {
        RouletteGame.ColorOf(0).Should().Be(RouletteColor.Zero);
        RouletteGame.ColorOf(1).Should().Be(RouletteColor.Red);
        RouletteGame.ColorOf(2).Should().Be(RouletteColor.Black);
        RouletteGame.ColorOf(36).Should().Be(RouletteColor.Red);
    }
}
