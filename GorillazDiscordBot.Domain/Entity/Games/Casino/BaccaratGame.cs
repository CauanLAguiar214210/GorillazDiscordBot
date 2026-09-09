using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum BaccaratBetType
{
    Player,
    Banker,
    Tie
}

public enum BaccaratOutcome
{
    Pending,
    PlayerWin,
    BankerWin,
    Tie
}

public sealed class BaccaratHand
{
    private readonly List<Card> _cards = new();

    public IReadOnlyList<Card> Cards => _cards;

    public int Value => _cards.Sum(ValueOf) % 10;

    public Card? ThirdCard => _cards.Count >= 3 ? _cards[2] : null;

    public bool IsNatural => Cards.Count == 2 && Value >= 8;

    internal void Add(Card card) => _cards.Add(card);

    public static int ValueOf(Card card) => card.Rank switch
    {
        Rank.Ace => 1,
        >= Rank.Two and <= Rank.Nine => (int)card.Rank,
        _ => 0
    };
}

public sealed class BaccaratGame
{
    private readonly Deck _deck = new();

    private readonly Func<Card> _nextCard;

    public ulong Bet { get; }

    public BaccaratBetType BetType { get; }

    public BaccaratHand Player { get; } = new();

    public BaccaratHand Banker { get; } = new();

    public bool HasRevealed { get; private set; }

    public BaccaratOutcome Outcome { get; private set; } = BaccaratOutcome.Pending;

    public bool IsWin => Outcome switch
    {
        BaccaratOutcome.PlayerWin when BetType == BaccaratBetType.Player => true,
        BaccaratOutcome.BankerWin when BetType == BaccaratBetType.Banker => true,
        BaccaratOutcome.Tie when BetType == BaccaratBetType.Tie => true,
        _ => false
    };

    public BaccaratGame(ulong bet, BaccaratBetType betType, Func<Card>? nextCard = null)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        Bet = bet;
        BetType = betType;
        _nextCard = nextCard ?? NextFromDeck;

        Player.Add(_nextCard());
        Banker.Add(_nextCard());
        Player.Add(_nextCard());
        Banker.Add(_nextCard());
    }

    public BaccaratOutcome Reveal()
    {
        if (HasRevealed)
            throw new InvalidOperationException("Esta rodada já foi revelada.");

        HasRevealed = true;

        var playerNatural = Player.Value >= 8;
        var bankerNatural = Banker.Value >= 8;

        if (!playerNatural && !bankerNatural)
        {
            var playerDraws = Player.Value <= 5;
            if (playerDraws)
                Player.Add(_nextCard());

            ResolveBankerDraw(playerDraws);
        }

        Outcome = Resolve();
        return Outcome;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasRevealed || !IsWin)
            return 0;

        return Outcome switch
        {
            BaccaratOutcome.PlayerWin => bet * 2,
            BaccaratOutcome.BankerWin => (ulong)Math.Floor(bet * CasinoRules.BaccaratBankerPayout),
            BaccaratOutcome.Tie => bet * (ulong)CasinoRules.BaccaratTiePayout,
            _ => 0
        };
    }

    public static bool ShouldBankerDraw(int bankerValue, bool playerDrew, int? playerThirdValue)
    {
        if (bankerValue == 7)
            return false;

        if (bankerValue <= 2)
            return true;

        if (!playerDrew)
            return bankerValue <= 5;

        var third = playerThirdValue!.Value;
        return bankerValue switch
        {
            3 => third != 8,
            4 => third is >= 2 and <= 7,
            5 => third is >= 4 and <= 7,
            6 => third is >= 6 and <= 7,
            _ => false
        };
    }

    private void ResolveBankerDraw(bool playerDrew)
    {
        var shouldDraw = ShouldBankerDraw(
            Banker.Value, playerDrew, Player.ThirdCard == null ? null : BaccaratHand.ValueOf(Player.ThirdCard));

        if (shouldDraw)
            Banker.Add(_nextCard());
    }

    private BaccaratOutcome Resolve() => Outcome = Player.Value == Banker.Value
        ? BaccaratOutcome.Tie
        : Player.Value > Banker.Value
            ? BaccaratOutcome.PlayerWin
            : BaccaratOutcome.BankerWin;

    private Card NextFromDeck()
    {
        if (_deck.RemainingCards == 0)
            _deck.Shuffle();

        return _deck.Draw();
    }
}