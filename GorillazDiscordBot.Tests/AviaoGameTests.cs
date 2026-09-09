using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class AviaoGameTests
{
    [Fact]
    public void Inicio_MultiplicadorComecaEmUm()
    {
        var game = new AviaoGame(100, () => 5.0);

        game.CurrentMultiplier.Should().Be(1.0);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Fly_AntesDoCrash_SubiOStep()
    {
        var game = new AviaoGame(100, () => 5.0);

        var crashed = game.Fly();

        crashed.Should().BeFalse();
        game.CurrentMultiplier.Should().Be(1.1);
        game.HasCrashed.Should().BeFalse();
    }

    [Fact]
    public void Fly_QuandoAtingeOCrashPoint_Explode()
    {
        var game = new AviaoGame(100, () => 1.5);

        for (var i = 0; i < 5; i++)
            game.Fly();

        game.HasCrashed.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
        game.CrashMultiplier.Should().Be(1.5);
        game.CurrentMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void CashOut_ResgataApostaVezesMultiplicador()
    {
        var game = new AviaoGame(100, () => 5.0);
        game.Fly();

        var amount = game.CashOut();

        amount.Should().Be(110);
        game.HasCashedOut.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void CashOut_Imediato_DevolveAposta()
    {
        var game = new AviaoGame(100, () => 5.0);

        var amount = game.CashOut();

        amount.Should().Be(100);
    }

    [Fact]
    public void CashOut_DepoisDoCrash_LancaExcecao()
    {
        var game = new AviaoGame(100, () => 1.1);
        game.Fly();
        game.HasCrashed.Should().BeTrue();

        var act = () => game.CashOut();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fly_DepoisDeAbrirMao_LancaExcecao()
    {
        var game = new AviaoGame(100, () => 5.0);
        game.CashOut();

        var act = () => game.Fly();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CrashPointForaDoIntervalo_SeraAjustado()
    {
        var game = new AviaoGame(100, () => 0.5);

        var crashed = game.Fly();

        crashed.Should().BeTrue();
        game.CrashMultiplier.Should().Be(CasinoRules.AviaoMinCrash);
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new AviaoGame(0, () => 5.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}