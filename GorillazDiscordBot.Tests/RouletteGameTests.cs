using FluentAssertions;
using GorillazDiscordBot.Domain.Games;

namespace GorillazDiscordBot.Tests;

public class RouletteGameTests
{
    [Fact]
    public void ApostaNumero_Acertar_Paga36x()
    {
        var game = new RouletteGame(17);
        game.Evaluate(RouletteBet.OnNumber(17)).Should().Be(36);
    }

    [Fact]
    public void ApostaNumero_Errar_Paga0()
    {
        var game = new RouletteGame(18);
        game.Evaluate(RouletteBet.OnNumber(17)).Should().Be(0);
    }

    [Fact]
    public void ApostaCor_Vermelho_Acertar_Paga2x()
    {
        var game = new RouletteGame(3); // 3 é vermelho
        game.Color.Should().Be(RouletteColor.Red);
        game.Evaluate(RouletteBet.OnColor(RouletteColor.Red)).Should().Be(2);
    }

    [Fact]
    public void ApostaCor_Preto_Acertar_Paga2x()
    {
        var game = new RouletteGame(17); // 17 é preto
        game.Color.Should().Be(RouletteColor.Black);
        game.Evaluate(RouletteBet.OnColor(RouletteColor.Black)).Should().Be(2);
    }

    [Fact]
    public void ApostaCor_Errada_Paga0()
    {
        var game = new RouletteGame(17); // 17 é preto
        game.Evaluate(RouletteBet.OnColor(RouletteColor.Red)).Should().Be(0);
    }

    [Fact]
    public void Zero_DeixaTodaApostaDeLadoPerdedora()
    {
        var game = new RouletteGame(0);
        game.Color.Should().Be(RouletteColor.Green);
        game.Evaluate(RouletteBet.OnColor(RouletteColor.Red)).Should().Be(0);
        game.Evaluate(RouletteBet.OnEvenOdd(true)).Should().Be(0);
        game.Evaluate(RouletteBet.OnHalf(true)).Should().Be(0);
    }

    [Fact]
    public void ApostaParImpar_Funciona()
    {
        new RouletteGame(4).Evaluate(RouletteBet.OnEvenOdd(true)).Should().Be(2);
        new RouletteGame(3).Evaluate(RouletteBet.OnEvenOdd(false)).Should().Be(2);
    }

    [Fact]
    public void ApostaAltoBaixo_Funciona()
    {
        new RouletteGame(5).Evaluate(RouletteBet.OnHalf(true)).Should().Be(2);
        new RouletteGame(20).Evaluate(RouletteBet.OnHalf(false)).Should().Be(2);
    }

    [Fact]
    public void Describe_RetornaRotuloLegivel()
    {
        RouletteBet.OnNumber(7).Describe().Should().Be("Número 7");
        RouletteBet.OnColor(RouletteColor.Black).Describe().Should().Be("Preto");
        RouletteBet.OnEvenOdd(true).Describe().Should().Be("Par");
        RouletteBet.OnHalf(false).Describe().Should().Be("Alto (19-36)");
    }
}
