using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum VideoPokerHandOutcome
{
    None,
    JacksOrBetter,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush
}

public sealed class VideoPokerGame
{
    private readonly Deck _deck;
    private readonly HashSet<int> _held = new();

    public IReadOnlyList<Card> Hand { get; }

    public IReadOnlyList<int> HeldPositions => _held.OrderBy(i => i).ToArray();

    public bool HasHeldCards => _held.Count > 0;

    public bool HasDrawn { get; private set; }

    public bool HasDiscarded => _held.Count < Hand.Count;

    public VideoPokerGame(Deck? deck = null)
    {
        _deck = deck ?? new Deck();

        var hand = new List<Card>(5);
        for (var i = 0; i < 5; i++)
            hand.Add(_deck.Draw());
        Hand = hand;
    }

    public void Hold(params int[] positions)
    {
        if (HasDrawn)
            throw new InvalidOperationException("As cartas já foram trocadas.");

        foreach (var position in positions)
        {
            if (position < 0 || position >= Hand.Count)
                throw new ArgumentOutOfRangeException(nameof(position), "Posição deve estar entre 0 e 4.");

            if (!_held.Add(position))
                _held.Remove(position);
        }
    }

    public IReadOnlyList<Card> Draw()
    {
        if (HasDrawn)
            throw new InvalidOperationException("As cartas já foram trocadas.");

        var replacements = new List<Card>();
        for (var i = 0; i < Hand.Count; i++)
        {
            if (_held.Contains(i))
                continue;

            var card = _deck.Draw();
            replacements.Add(card);
            ((List<Card>)Hand)[i] = card;
        }

        HasDrawn = true;
        return Hand;
    }

    public VideoPokerHandOutcome Evaluate()
        => EvaluateHand(Hand);

    public static VideoPokerHandOutcome EvaluateHand(IReadOnlyList<Card> hand)
    {
        if (hand.Count != 5)
            throw new ArgumentException("Uma mão de poker deve ter exatamente 5 cartas.");

        var counts = hand
            .GroupBy(c => c.Rank)
            .ToDictionary(g => g.Key, g => g.Count());

        var isFlush = hand.All(c => c.Suit == hand[0].Suit);
        var isStraight = IsStraight(counts.Keys.ToList());

        if (isFlush && isStraight)
            return counts.ContainsKey(Rank.Ace) && counts.ContainsKey(Rank.Ten)
                ? VideoPokerHandOutcome.RoyalFlush
                : VideoPokerHandOutcome.StraightFlush;

        if (counts.ContainsValue(4))
            return VideoPokerHandOutcome.FourOfAKind;

        if (counts.ContainsValue(3) && counts.ContainsValue(2))
            return VideoPokerHandOutcome.FullHouse;

        if (isFlush)
            return VideoPokerHandOutcome.Flush;

        if (isStraight)
            return VideoPokerHandOutcome.Straight;

        if (counts.ContainsValue(3))
            return VideoPokerHandOutcome.ThreeOfAKind;

        if (counts.Count(c => c.Value == 2) == 2)
            return VideoPokerHandOutcome.TwoPair;

        var pairRank = counts.FirstOrDefault(c => c.Value == 2).Key;
        if (pairRank is >= Rank.Jack)
            return VideoPokerHandOutcome.JacksOrBetter;

        return VideoPokerHandOutcome.None;
    }

    public static ulong CalculateReturn(VideoPokerHandOutcome outcome, ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        return bet * (ulong)Multiplier(outcome);
    }

    public static int Multiplier(VideoPokerHandOutcome outcome) => outcome switch
    {
        VideoPokerHandOutcome.RoyalFlush => 250,
        VideoPokerHandOutcome.StraightFlush => 50,
        VideoPokerHandOutcome.FourOfAKind => 25,
        VideoPokerHandOutcome.FullHouse => 9,
        VideoPokerHandOutcome.Flush => 6,
        VideoPokerHandOutcome.Straight => 4,
        VideoPokerHandOutcome.ThreeOfAKind => 3,
        VideoPokerHandOutcome.TwoPair => 2,
        VideoPokerHandOutcome.JacksOrBetter => 1,
        _ => 0
    };

    private static bool IsStraight(IReadOnlyList<Rank> ranks)
    {
        if (ranks.Count != 5)
            return false;

        var sorted = ranks.Select(r => (int)r).OrderBy(v => v).ToList();

        if (sorted[4] - sorted[0] == 4 && sorted.Distinct().Count() == 5)
            return true;

        return sorted.SequenceEqual(new[] { 2, 3, 4, 5, 14 });
    }
}