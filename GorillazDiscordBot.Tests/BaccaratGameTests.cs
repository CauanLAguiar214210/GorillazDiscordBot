using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class BaccaratGameTests
{
    [Fact]
    public void Construtor_DistribuiDuasCartasPorLado()
    {
        var game = new BaccaratGame(100, BaccaratBetType.Player, MakeDeck(Card(Rank.Ace), Card(Rank.King), Card(Rank.Two), Card(Rank.Three)));

        game.Player.Cards.Should().HaveCount(2);
        game.Banker.Cards.Should().HaveCount(2);
        game.HasRevealed.Should().BeFalse();
    }

    [Fact]
    public void ValorDaMao_SomaModuloDez()
    {
        BaccaratHand.ValueOf(Card(Rank.Ace)).Should().Be(1);
        BaccaratHand.ValueOf(Card(Rank.Nine)).Should().Be(9);
        BaccaratHand.ValueOf(Card(Rank.Ten)).Should().Be(0);
        BaccaratHand.ValueOf(Card(Rank.King)).Should().Be(0);
    }

    [Fact]
    public void NaturalDoJogador_EncerraSemTerceiraCarta()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Player,
            MakeDeck(Card(Rank.Five), Card(Rank.King), Card(Rank.Three), Card(Rank.Queen), Card(Rank.Six)));

        var outcome = game.Reveal();

        outcome.Should().Be(BaccaratOutcome.PlayerWin);
        game.Player.Cards.Should().HaveCount(2);
        game.Banker.Cards.Should().HaveCount(2);
        game.CalculateReturn(100).Should().Be(200);
    }

    [Fact]
    public void Banco_PuxaPelaTerceiraCartaeVence()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Banker,
            MakeDeck(
                Card(Rank.Two), Card(Rank.Four), Card(Rank.Two),
                Card(Rank.King), Card(Rank.Two), Card(Rank.Five)));

        var outcome = game.Reveal();

        outcome.Should().Be(BaccaratOutcome.BankerWin);
        game.Player.Cards.Should().HaveCount(3);
        game.Banker.Cards.Should().HaveCount(3);
        game.CalculateReturn(100).Should().Be(195);
    }

    [Fact]
    public void Empate_PagaNoveVezes()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Tie,
            MakeDeck(Card(Rank.Two), Card(Rank.Five), Card(Rank.King), Card(Rank.King), Card(Rank.Three)));

        var outcome = game.Reveal();

        outcome.Should().Be(BaccaratOutcome.Tie);
        game.Player.Cards.Should().HaveCount(3);
        game.Banker.Cards.Should().HaveCount(2);
        game.CalculateReturn(100).Should().Be(900);
    }

    [Fact]
    public void ApostaErrada_NaoGanha()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Player,
            MakeDeck(Card(Rank.Two), Card(Rank.Four), Card(Rank.Two), Card(Rank.King), Card(Rank.Two), Card(Rank.Five)));

        game.Reveal();

        game.Outcome.Should().Be(BaccaratOutcome.BankerWin);
        game.IsWin.Should().BeFalse();
        game.CalculateReturn(100).Should().Be(0);
    }

    [Fact]
    public void BancoNaoPuxa_QuandoJogadorNaoPuxou()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Player,
            MakeDeck(Card(Rank.Seven), Card(Rank.Three), Card(Rank.Queen), Card(Rank.Four)));

        game.Reveal();

        game.Banker.Cards.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(7, true, 6, false)]
    [InlineData(2, false, null, true)]
    [InlineData(3, true, 8, false)]
    [InlineData(4, true, 8, false)]
    [InlineData(4, true, 2, true)]
    [InlineData(5, true, 3, false)]
    [InlineData(5, true, 6, true)]
    [InlineData(6, true, 5, false)]
    [InlineData(6, true, 6, true)]
    [InlineData(6, false, null, false)]
    public void ShouldBankerDraw_RegraOficial(int bankerValue, bool playerDrew, int? third, bool expected)
    {
        BaccaratGame.ShouldBankerDraw(bankerValue, playerDrew, third).Should().Be(expected);
    }

    [Fact]
    public void Reveal_Duplicado_LancaExcecao()
    {
        var game = new BaccaratGame(
            100, BaccaratBetType.Player,
            MakeDeck(Card(Rank.Five), Card(Rank.King), Card(Rank.Three), Card(Rank.Queen)));

        game.Reveal();

        var act = () => game.Reveal();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new BaccaratGame(0, BaccaratBetType.Player, () => Card(Rank.Seven));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Card Card(Rank rank) => new(Suit.Spades, rank);

    private static Func<Card> MakeDeck(params Card[] cards)
    {
        var queue = new Queue<Card>(cards);
        return () => queue.Dequeue();
    }
}