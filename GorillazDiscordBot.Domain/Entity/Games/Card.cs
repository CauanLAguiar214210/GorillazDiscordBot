namespace GorillazDiscordBot.Domain.Entity.Games;

public enum Suit
{
    Spades,
    Hearts,
    Diamonds,
    Clubs
}

public enum Rank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public record Card(Suit Suit, Rank Rank)
{
    public string Symbol => $"{RankSymbol}{SuitSymbol}";

    public char SuitSymbol => Suit switch
    {
        Suit.Spades => '\u2660',
        Suit.Hearts => '\u2665',
        Suit.Diamonds => '\u2666',
        Suit.Clubs => '\u2663',
        _ => '?'
    };

    public string RankSymbol => Rank switch
    {
        Rank.Jack => "J",
        Rank.Queen => "Q",
        Rank.King => "K",
        Rank.Ace => "A",
        _ => ((int)Rank).ToString()
    };
}
