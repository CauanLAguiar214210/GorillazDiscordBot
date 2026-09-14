using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Utils;

/// <summary>Ajuda de renderização de cartas vindas do microserviço (nunca inclui a carta oculta).</summary>
public static class CasinoCards
{
    public const string CardBack = "\U0001F0A0";

    public static string CardText(CardDto card)
        => $"{RankText(card.Rank)}{SuitSymbol(card.Suit)}";

    public static string DescribeCard(CardDto card)
        => $"{RankName(card.Rank)} de {SuitName(card.Suit)}";

    public static string CardsText(IReadOnlyList<CardDto> cards)
        => cards.Count == 0
            ? ""
            : string.Join(" ", cards.Select(c => $"`{CardText(c)}`"));

    private static char SuitSymbol(Suit suit) => suit switch
    {
        Suit.Spades => '\u2660',
        Suit.Hearts => '\u2665',
        Suit.Diamonds => '\u2666',
        Suit.Clubs => '\u2663',
        _ => '?'
    };

    private static string RankText(Rank rank) => rank switch
    {
        Rank.Jack => "J",
        Rank.Queen => "Q",
        Rank.King => "K",
        Rank.Ace => "A",
        _ => ((int)rank).ToString()
    };

    private static string RankName(Rank rank) => rank switch
    {
        Rank.Ace => "Ás",
        Rank.King => "Rei",
        Rank.Queen => "Dama",
        Rank.Jack => "Valete",
        Rank.Ten => "10",
        _ => ((int)rank).ToString()
    };

    private static string SuitName(Suit suit) => suit switch
    {
        Suit.Spades => "♠",
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs => "♣",
        _ => "?"
    };
}