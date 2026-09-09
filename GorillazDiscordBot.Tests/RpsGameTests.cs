using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class RpsGameTests
{
    [Fact]
    public void Play_PlayerGanha_DevolveESetaResultado()
    {
        var game = new RpsGame(RpsMove.Pedra, () => RpsMove.Tesoura);

        var opponent = game.Play();

        opponent.Should().Be(RpsMove.Tesoura);
        game.Outcome.Should().Be(RpsOutcome.PlayerWin);
        game.IsWin.Should().BeTrue();
        game.CalculateReturn(100).Should().Be(200);
    }

    [Fact]
    public void Play_Empate_DevolveAposta()
    {
        var game = new RpsGame(RpsMove.Papel, () => RpsMove.Papel);

        game.Play();

        game.Outcome.Should().Be(RpsOutcome.Draw);
        game.IsDraw.Should().BeTrue();
        game.IsWin.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(100);
    }

    [Fact]
    public void Play_PlayerPerde_RetornaZero()
    {
        var game = new RpsGame(RpsMove.Pedra, () => RpsMove.Papel);

        game.Play();

        game.Outcome.Should().Be(RpsOutcome.PlayerLose);
        game.IsDraw.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void Play_Duplicado_LancaExcecao()
    {
        var game = new RpsGame(RpsMove.Pedra, () => RpsMove.Tesoura);
        game.Play();

        var act = () => game.Play();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CalcularRetornoSemJogar_RetornaZero()
    {
        var game = new RpsGame(RpsMove.Pedra, () => RpsMove.Tesoura);

        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void Resolve_PedraVenceTesoura()
    {
        RpsGame.Resolve(RpsMove.Pedra, RpsMove.Tesoura).Should().Be(RpsOutcome.PlayerWin);
    }

    [Fact]
    public void Resolve_TesouraVencePapel()
    {
        RpsGame.Resolve(RpsMove.Tesoura, RpsMove.Papel).Should().Be(RpsOutcome.PlayerWin);
    }

    [Fact]
    public void Resolve_PapelVencePedra()
    {
        RpsGame.Resolve(RpsMove.Papel, RpsMove.Pedra).Should().Be(RpsOutcome.PlayerWin);
    }

    [Fact]
    public void Resolve_Igual_Empata()
    {
        RpsGame.Resolve(RpsMove.Tesoura, RpsMove.Tesoura).Should().Be(RpsOutcome.Draw);
    }
}