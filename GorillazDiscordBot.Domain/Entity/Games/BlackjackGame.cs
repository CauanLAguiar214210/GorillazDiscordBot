namespace GorillazDiscordBot.Domain.Entity.Games;

public enum BlackjackPhase
{
    PlayerTurn,
    Finished
}

public enum BlackjackOutcome
{
    Pending,
    PlayerWin,
    DealerWin,
    Push,
    PlayerBlackjack
}

public sealed class BlackjackHand
{
    private readonly List<Card> _cards = new();

    public IReadOnlyList<Card> Cards => _cards;

    public int Value
    {
        get
        {
            var total = 0;
            var aces = 0;

            foreach (var card in _cards)
            {
                if (card.Rank == Rank.Ace)
                {
                    aces++;
                    total += 11;
                }
                else if (card.Rank >= Rank.Jack)
                {
                    total += 10;
                }
                else
                {
                    total += (int)card.Rank;
                }
            }

            while (total > BlackjackGame.MaxValue && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return total;
        }
    }

    public bool IsBust => Value > BlackjackGame.MaxValue;

    public bool IsBlackjack => _cards.Count == 2 && Value == BlackjackGame.MaxValue;

    internal void Add(Card card) => _cards.Add(card);
}

public sealed class BlackjackGame
{
    public const int MaxValue = 21;
    public const int DealerStandValue = 17;

    private readonly Deck _deck;

    public int Bet { get; private set; }
    public BlackjackHand Player { get; } = new();
    public BlackjackHand Dealer { get; } = new();
    public BlackjackPhase Phase { get; private set; } = BlackjackPhase.PlayerTurn;
    public BlackjackOutcome Outcome { get; private set; } = BlackjackOutcome.Pending;
    public bool Doubled { get; private set; }

    public bool DealerHoleHidden => Phase == BlackjackPhase.PlayerTurn;

    public BlackjackGame(int bet, Deck? deck = null)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        Bet = bet;
        _deck = deck ?? new Deck();

        Player.Add(_deck.Draw());
        Dealer.Add(_deck.Draw());
        Player.Add(_deck.Draw());
        Dealer.Add(_deck.Draw());

        ResolveNaturals();
    }

    public void Hit()
    {
        EnsurePlayerTurn();

        Player.Add(_deck.Draw());

        if (Player.IsBust)
        {
            Finish(BlackjackOutcome.DealerWin);
            return;
        }

        if (Player.Value == MaxValue)
        {
            Stand();
        }
    }

    public void Stand()
    {
        EnsurePlayerTurn();

        Phase = BlackjackPhase.Finished;

        while (Dealer.Value < DealerStandValue)
            Dealer.Add(_deck.Draw());

        Outcome = ResolveShowdown();
    }

    public void DoubleDown()
    {
        EnsurePlayerTurn();

        if (Player.Cards.Count != 2 || Doubled)
            throw new InvalidOperationException("Dobrar é permitido apenas nas duas primeiras cartas.");

        Bet *= 2;
        Doubled = true;

        Player.Add(_deck.Draw());

        if (Player.IsBust)
        {
            Finish(BlackjackOutcome.DealerWin);
            return;
        }

        Stand();
    }

    public int CalculateTotalReturn() => Outcome switch
    {
        BlackjackOutcome.PlayerBlackjack => Bet + (int)Math.Floor(Bet * 1.5m),
        BlackjackOutcome.PlayerWin => Bet * 2,
        BlackjackOutcome.Push => Bet,
        _ => 0
    };

    private void ResolveNaturals()
    {
        if (!Player.IsBlackjack && !Dealer.IsBlackjack)
            return;

        if (Player.IsBlackjack && Dealer.IsBlackjack)
        {
            Finish(BlackjackOutcome.Push);
            return;
        }

        Finish(Player.IsBlackjack ? BlackjackOutcome.PlayerBlackjack : BlackjackOutcome.DealerWin);
    }

    private BlackjackOutcome ResolveShowdown()
    {
        if (Dealer.IsBust || Player.Value > Dealer.Value)
            return BlackjackOutcome.PlayerWin;

        return Player.Value == Dealer.Value ? BlackjackOutcome.Push : BlackjackOutcome.DealerWin;
    }

    private void Finish(BlackjackOutcome outcome)
    {
        Phase = BlackjackPhase.Finished;
        Outcome = outcome;
    }

    private void EnsurePlayerTurn()
    {
        if (Phase != BlackjackPhase.PlayerTurn)
            throw new InvalidOperationException("Esta mão já foi encerrada.");
    }
}
