using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum HighLowGuessResult
{
    Win,
    Lose,
    Tie
}

public sealed class HighLowGame
{
    public const double MaxMultiplier = 10_000;

    private readonly Deck _deck = new();

    private readonly Func<Card> _nextCard;

    public ulong Bet { get; }

    public Card CurrentCard { get; private set; }

    public double Multiplier { get; private set; } = 1.0;

    public bool HasLost { get; private set; }

    public bool HasCashedOut { get; private set; }

    public bool IsFinished => HasLost || HasCashedOut;

    public bool CanPickHigher => CurrentCard.Rank != Rank.Ace;

    public bool CanPickLower => CurrentCard.Rank != Rank.Two;

    public HighLowGame(ulong bet, Func<Card>? nextCard = null)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        Bet = bet;
        _nextCard = nextCard ?? NextFromDeck;
        CurrentCard = _nextCard();
    }

    public HighLowGuessResult Guess(bool higher)
    {
        if (IsFinished)
            throw new InvalidOperationException("Esta rodada já encerrou.");

        if (higher ? !CanPickHigher : !CanPickLower)
        {
            HasLost = true;
            return HighLowGuessResult.Lose;
        }

        var drawn = _nextCard();

        if (drawn.Rank == CurrentCard.Rank)
        {
            CurrentCard = drawn;
            return HighLowGuessResult.Tie;
        }

        var isWin = higher ? drawn.Rank > CurrentCard.Rank : drawn.Rank < CurrentCard.Rank;

        if (!isWin)
        {
            HasLost = true;
            CurrentCard = drawn;
            return HighLowGuessResult.Lose;
        }

        CurrentCard = drawn;
        Multiplier = Math.Min(Multiplier + CasinoRules.HighLowMultiplierStep, MaxMultiplier);
        return HighLowGuessResult.Win;
    }

    public ulong CashOut()
    {
        if (IsFinished)
            throw new InvalidOperationException("Esta rodada já encerrou.");

        HasCashedOut = true;
        return (ulong)Math.Floor((decimal)Bet * (decimal)Multiplier);
    }

    private Card NextFromDeck()
    {
        if (_deck.RemainingCards == 0)
            _deck.Shuffle();

        return _deck.Draw();
    }
}