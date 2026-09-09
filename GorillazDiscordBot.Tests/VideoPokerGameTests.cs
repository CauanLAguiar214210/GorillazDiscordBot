using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;
using GorillazDiscordBot.Domain.Entity.Games.Casino;

namespace GorillazDiscordBot.Tests;

public class VideoPokerGameTests
{
    private static Card C(Suit suit, Rank rank) => new(suit, rank);

    private static Deck SpareDeck()
        => new(new[]
        {
            C(Suit.Spades, Rank.Jack), C(Suit.Hearts, Rank.Jack), C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Seven), C(Suit.Hearts, Rank.Two),
            C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.Ace), C(Suit.Clubs, Rank.King), C(Suit.Hearts, Rank.King),
            C(Suit.Diamonds, Rank.Ten), C(Suit.Clubs, Rank.Nine), C(Suit.Hearts, Rank.Eight)
        });

    [Fact]
    public void EvaluateHand_RoyalFlush()
    {
        var hand = new[] { C(Suit.Spades, Rank.Ten), C(Suit.Spades, Rank.Jack), C(Suit.Spades, Rank.Queen), C(Suit.Spades, Rank.King), C(Suit.Spades, Rank.Ace) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.RoyalFlush);
    }

    [Fact]
    public void EvaluateHand_StraightFlush()
    {
        var hand = new[] { C(Suit.Hearts, Rank.Five), C(Suit.Hearts, Rank.Six), C(Suit.Hearts, Rank.Seven), C(Suit.Hearts, Rank.Eight), C(Suit.Hearts, Rank.Nine) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.StraightFlush);
    }

    [Fact]
    public void EvaluateHand_FourOfAKind()
    {
        var hand = new[] { C(Suit.Spades, Rank.Nine), C(Suit.Hearts, Rank.Nine), C(Suit.Diamonds, Rank.Nine), C(Suit.Clubs, Rank.Nine), C(Suit.Hearts, Rank.Two) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.FourOfAKind);
    }

    [Fact]
    public void EvaluateHand_FullHouse()
    {
        var hand = new[] { C(Suit.Spades, Rank.King), C(Suit.Hearts, Rank.King), C(Suit.Diamonds, Rank.King), C(Suit.Clubs, Rank.Three), C(Suit.Diamonds, Rank.Three) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.FullHouse);
    }

    [Fact]
    public void EvaluateHand_Flush()
    {
        var hand = new[] { C(Suit.Clubs, Rank.Two), C(Suit.Clubs, Rank.Five), C(Suit.Clubs, Rank.Nine), C(Suit.Clubs, Rank.Jack), C(Suit.Clubs, Rank.Ace) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.Flush);
    }

    [Fact]
    public void EvaluateHand_Straight()
    {
        var hand = new[] { C(Suit.Clubs, Rank.Three), C(Suit.Hearts, Rank.Four), C(Suit.Diamonds, Rank.Five), C(Suit.Spades, Rank.Six), C(Suit.Clubs, Rank.Seven) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.Straight);
    }

    [Fact]
    public void EvaluateHand_StraightComAsBaixo()
    {
        var hand = new[] { C(Suit.Clubs, Rank.Ace), C(Suit.Hearts, Rank.Two), C(Suit.Diamonds, Rank.Three), C(Suit.Spades, Rank.Four), C(Suit.Clubs, Rank.Five) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.Straight);
    }

    [Fact]
    public void EvaluateHand_ThreeOfAKind()
    {
        var hand = new[] { C(Suit.Spades, Rank.Seven), C(Suit.Hearts, Rank.Seven), C(Suit.Diamonds, Rank.Seven), C(Suit.Clubs, Rank.Two), C(Suit.Spades, Rank.Five) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.ThreeOfAKind);
    }

    [Fact]
    public void EvaluateHand_TwoPair()
    {
        var hand = new[] { C(Suit.Spades, Rank.Four), C(Suit.Hearts, Rank.Four), C(Suit.Diamonds, Rank.Nine), C(Suit.Clubs, Rank.Nine), C(Suit.Hearts, Rank.Two) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.TwoPair);
    }

    [Fact]
    public void EvaluateHand_JacksOrBetter()
    {
        var hand = new[] { C(Suit.Spades, Rank.Jack), C(Suit.Hearts, Rank.Jack), C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Seven), C(Suit.Hearts, Rank.Two) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.JacksOrBetter);
    }

    [Fact]
    public void EvaluateHand_ParBaixoSemGanho()
    {
        var hand = new[] { C(Suit.Spades, Rank.Ten), C(Suit.Hearts, Rank.Ten), C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Seven), C(Suit.Hearts, Rank.Two) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.None);
    }

    [Fact]
    public void EvaluateHand_NenhumaCombinacao()
    {
        var hand = new[] { C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Five), C(Suit.Diamonds, Rank.Nine), C(Suit.Clubs, Rank.Jack), C(Suit.Hearts, Rank.King) };

        VideoPokerGame.EvaluateHand(hand).Should().Be(VideoPokerHandOutcome.None);
    }

    [Theory]
    [InlineData(VideoPokerHandOutcome.RoyalFlush, 250)]
    [InlineData(VideoPokerHandOutcome.StraightFlush, 50)]
    [InlineData(VideoPokerHandOutcome.FourOfAKind, 25)]
    [InlineData(VideoPokerHandOutcome.FullHouse, 9)]
    [InlineData(VideoPokerHandOutcome.Flush, 6)]
    [InlineData(VideoPokerHandOutcome.Straight, 4)]
    [InlineData(VideoPokerHandOutcome.ThreeOfAKind, 3)]
    [InlineData(VideoPokerHandOutcome.TwoPair, 2)]
    [InlineData(VideoPokerHandOutcome.JacksOrBetter, 1)]
    [InlineData(VideoPokerHandOutcome.None, 0)]
    public void Multiplier_RetornaValorEsperado(VideoPokerHandOutcome outcome, int expected)
    {
        VideoPokerGame.Multiplier(outcome).Should().Be(expected);
    }

    [Fact]
    public void CalculoRetorno_PagaApostaVezesMultiplicador()
    {
        VideoPokerGame.CalculateReturn(VideoPokerHandOutcome.FullHouse, 100).Should().Be(900);
    }

    [Fact]
    public void Draw_SubstituiApenasCartasNaoSeguradas()
    {
        var deck = new Deck(new[]
        {
            C(Suit.Spades, Rank.Jack), C(Suit.Hearts, Rank.Jack), C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Seven), C(Suit.Hearts, Rank.Ace),
            C(Suit.Diamonds, Rank.Nine), C(Suit.Clubs, Rank.Nine), C(Suit.Hearts, Rank.Two)
        });
        var game = new VideoPokerGame(deck);
        game.Hold(0, 1);

        var hand = game.Draw();

        hand[0].Should().Be(C(Suit.Spades, Rank.Jack));
        hand[1].Should().Be(C(Suit.Hearts, Rank.Jack));
        hand[2].Should().Be(C(Suit.Diamonds, Rank.Nine));
        hand[3].Should().Be(C(Suit.Clubs, Rank.Nine));
        hand[4].Should().Be(C(Suit.Hearts, Rank.Two));
        game.HasDrawn.Should().BeTrue();
        game.Evaluate().Should().Be(VideoPokerHandOutcome.TwoPair);
    }

    [Fact]
    public void Hold_SeguraETiraToggle()
    {
        var deck = new Deck(new[]
        {
            C(Suit.Spades, Rank.Jack), C(Suit.Hearts, Rank.Jack), C(Suit.Diamonds, Rank.Three), C(Suit.Clubs, Rank.Seven), C(Suit.Hearts, Rank.Two),
            C(Suit.Spades, Rank.Ace), C(Suit.Hearts, Rank.Ace), C(Suit.Diamonds, Rank.Ace)
        });
        var game = new VideoPokerGame(deck);
        game.Hold(0);
        game.Hold(0);
        game.Hold(2);

        game.HeldPositions.Should().BeEquivalentTo(new[] { 2 });
    }

    [Fact]
    public void Draw_DuasVezes_LancaExcecao()
    {
        var game = new VideoPokerGame(SpareDeck());
        game.Draw();

        var act = () => game.Draw();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Hold_DepoisDeDraw_LancaExcecao()
    {
        var game = new VideoPokerGame(SpareDeck());
        game.Draw();

        var act = () => game.Hold(0);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Hold_PosicaoInvalida_LancaExcecao()
    {
        var game = new VideoPokerGame();

        var act = () => game.Hold(9);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => VideoPokerGame.CalculateReturn(VideoPokerHandOutcome.FullHouse, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EvaluateHand_MaoInvalida_LancaExcecao()
    {
        var hand = new[] { C(Suit.Spades, Rank.Two), C(Suit.Hearts, Rank.Three) };

        var act = () => VideoPokerGame.EvaluateHand(hand);

        act.Should().Throw<ArgumentException>();
    }
}