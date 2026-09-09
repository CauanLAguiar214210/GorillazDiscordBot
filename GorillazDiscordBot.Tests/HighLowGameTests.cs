using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class HighLowGameTests
{
    [Fact]
    public void Construtor_SetaCartaAtual()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven)));

        game.CurrentCard.Should().Be(Card(Rank.Seven));
        game.Multiplier.Should().Be(1.0);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Guess_Acerto_MultiplicaEMudaCarta()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven), Card(Rank.Nine)));

        var result = game.Guess(true);

        result.Should().Be(HighLowGuessResult.Win);
        game.CurrentCard.Should().Be(Card(Rank.Nine));
        game.Multiplier.Should().Be(2.0);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Guess_Erro_Perde()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven), Card(Rank.Five)));

        var result = game.Guess(true);

        result.Should().Be(HighLowGuessResult.Lose);
        game.HasLost.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Guess_Igual_EmpataSemPenalidade()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven), Card(Rank.Seven)));

        var result = game.Guess(true);

        result.Should().Be(HighLowGuessResult.Tie);
        game.Multiplier.Should().Be(1.0);
        game.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void Guess_LadoImpossivel_PerdeNaHora()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Ace)));

        var result = game.Guess(true);

        result.Should().Be(HighLowGuessResult.Lose);
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void CanPickLower_FalsoNoDois()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Two)));

        game.CanPickHigher.Should().BeTrue();
        game.CanPickLower.Should().BeFalse();
    }

    [Fact]
    public void CashOut_ResgataApostaVezesMultiplicador()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven), Card(Rank.Nine), Card(Rank.Jack)));
        game.Guess(true);
        game.Guess(true);

        var amount = game.CashOut();

        amount.Should().Be(400);
        game.HasCashedOut.Should().BeTrue();
        game.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Guess_DepoisDeSair_LancaExcecao()
    {
        var game = new HighLowGame(100, MakeDeck(Card(Rank.Seven)));
        game.CashOut();

        var act = () => game.Guess(true);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new HighLowGame(0, () => Card(Rank.Seven));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static Card Card(Rank rank) => new(Suit.Hearts, rank);

    private static Func<Card> MakeDeck(params Card[] cards)
    {
        var queue = new Queue<Card>(cards);
        return () => queue.Dequeue();
    }
}