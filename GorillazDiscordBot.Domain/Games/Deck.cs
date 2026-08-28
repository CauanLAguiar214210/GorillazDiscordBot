namespace GorillazDiscordBot.Domain.Games;

public sealed class Deck
{
    private const int StandardSize = 52;

    private readonly List<Card> _cards = new(StandardSize);
    private int _next;

    public Deck()
        : this(CreateStandardOrder())
    {
        Shuffle();
    }

    public Deck(IEnumerable<Card> cards)
    {
        _cards.AddRange(cards);
    }

    public int RemainingCards => _cards.Count - _next;

    public Card Draw()
    {
        if (_next >= _cards.Count)
            throw new InvalidOperationException("O baralho acabou.");

        return _cards[_next++];
    }

    public void Shuffle(Random? random = null)
    {
        random ??= Random.Shared;

        for (var i = _cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }

        _next = 0;
    }

    private static IEnumerable<Card> CreateStandardOrder()
    {
        foreach (var suit in Enum.GetValues<Suit>())
            foreach (var rank in Enum.GetValues<Rank>())
                yield return new Card(suit, rank);
    }
}
