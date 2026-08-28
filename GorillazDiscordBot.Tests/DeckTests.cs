using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Tests;

public class DeckTests
{
    [Fact]
    public void DeckPadrao_Tem52CartasUnicas()
    {
        var deck = new Deck();

        var drawn = Enumerable.Range(0, 52).Select(_ => deck.Draw()).ToList();

        drawn.Should().HaveCount(52);
        drawn.Distinct().Should().HaveCount(52);
        deck.RemainingCards.Should().Be(0);
    }

    [Fact]
    public void Draw_BaralhoVazio_LancaExcecao()
    {
        var deck = new Deck();

        var act = () =>
        {
            foreach (var _ in Enumerable.Range(0, 53))
                deck.Draw();
        };

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DeckComOrdemCustom_DesenhaNaOrdemInformada()
    {
        var esperado = new[]
        {
            new Card(Suit.Spades, Rank.Ace),
            new Card(Suit.Hearts, Rank.Two),
            new Card(Suit.Clubs, Rank.King)
        };

        var deck = new Deck(esperado);

        deck.Draw().Should().Be(esperado[0]);
        deck.Draw().Should().Be(esperado[1]);
        deck.Draw().Should().Be(esperado[2]);
    }

    [Fact]
    public void Shuffle_MantemAsMesmas52Cartas()
    {
        var antes = CaptureCards(new Deck());

        var deck = new Deck();
        deck.Shuffle(new Random(12345));
        var depois = CaptureCards(deck);

        depois.Should().BeEquivalentTo(antes);
    }

    private static List<Card> CaptureCards(Deck deck)
        => Enumerable.Range(0, 52).Select(_ => deck.Draw()).ToList();
}
