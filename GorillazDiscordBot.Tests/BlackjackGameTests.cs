using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Games;

namespace GorillazDiscordBot.Tests;

public class BlackjackGameTests
{
    private const int Bet = 100;

    [Fact]
    public void Constructor_ApostaNaoPositiva_LancaExcecao()
    {
        var act = () => new BlackjackGame(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_Distribui2CartasParaCadaUm()
    {
        var game = CreateGame(Rank.Ten, Rank.Five, Rank.Nine, Rank.Six);

        game.Player.Cards.Should().HaveCount(2);
        game.Dealer.Cards.Should().HaveCount(2);
        game.Phase.Should().Be(BlackjackPhase.PlayerTurn);
        game.Outcome.Should().Be(BlackjackOutcome.Pending);
    }

    [Fact]
    public void BlackjackNatural_PagaTresPorDois()
    {
        var game = CreateGame(Rank.Ace, Rank.Queen, Rank.King, Rank.Three);

        game.Outcome.Should().Be(BlackjackOutcome.PlayerBlackjack);
        game.Phase.Should().Be(BlackjackPhase.Finished);
        game.CalculateTotalReturn().Should().Be(Bet + 150);
    }

    [Fact]
    public void BlackjackNatural_ComApostaImpar_ArredondaPagamentoParaBaixo()
    {
        var game = new BlackjackGame(101, DeckOf(
            C(Suit.Spades, Rank.Ace), C(Suit.Diamonds, Rank.Queen),
            C(Suit.Hearts, Rank.King), C(Suit.Clubs, Rank.Three)));

        game.CalculateTotalReturn().Should().Be(101 + 151);
    }

    [Fact]
    public void AmbosComBlackjack_EmpataEDevolveAposta()
    {
        var game = CreateGame(Rank.Ace, Rank.Ace, Rank.King, Rank.King);

        game.Outcome.Should().Be(BlackjackOutcome.Push);
        game.CalculateTotalReturn().Should().Be(Bet);
    }

    [Fact]
    public void DealerComBlackjack_VenceDireto()
    {
        var game = CreateGame(Rank.Ten, Rank.Ace, Rank.Nine, Rank.King);

        game.Outcome.Should().Be(BlackjackOutcome.DealerWin);
        game.Phase.Should().Be(BlackjackPhase.Finished);
        game.CalculateTotalReturn().Should().Be(0);
    }

    [Fact]
    public void Hit_Estourou_EncerraMaoComoDerrota()
    {
        var game = CreateGame(Rank.Ten, Rank.Five, Rank.Nine, Rank.Six, Rank.King);

        game.Hit();

        game.Player.IsBust.Should().BeTrue();
        game.Outcome.Should().Be(BlackjackOutcome.DealerWin);
        game.CalculateTotalReturn().Should().Be(0);
    }

    [Fact]
    public void Hit_AoChegarEm21_ParaAutomaticamenteEDealerJoga()
    {
        var game = CreateGame(Rank.Ten, Rank.Five, Rank.Nine, Rank.Six, Rank.Two, Rank.Seven);

        game.Hit();

        game.Player.Value.Should().Be(21);
        game.Dealer.Value.Should().Be(18);
        game.Outcome.Should().Be(BlackjackOutcome.PlayerWin);
    }

    [Fact]
    public void Stand_DealerCompraAbaixoDe17EVenceSeTiverMais()
    {
        var game = CreateGame(Rank.Ten, Rank.Five, Rank.Nine, Rank.Six, Rank.Queen);

        game.Stand();

        game.Dealer.Value.Should().Be(21);
        game.Outcome.Should().Be(BlackjackOutcome.DealerWin);
    }

    [Fact]
    public void Stand_DealerParaNo17Suave()
    {
        var game = CreateGame(Rank.Ten, Rank.Ace, Rank.Queen, Rank.Six);

        game.Stand();

        game.Dealer.Value.Should().Be(17);
        game.Dealer.Cards.Should().HaveCount(2);
        game.Outcome.Should().Be(BlackjackOutcome.PlayerWin);
    }

    [Fact]
    public void Stand_DealerEstourou_JogadorVence()
    {
        var game = CreateGame(Rank.Seven, Rank.Six, Rank.Eight, Rank.Six, Rank.Ten);

        game.Stand();

        game.Dealer.IsBust.Should().BeTrue();
        game.Outcome.Should().Be(BlackjackOutcome.PlayerWin);
        game.CalculateTotalReturn().Should().Be(Bet * 2);
    }

    [Fact]
    public void Stand_ValoresIguais_Empata()
    {
        var game = CreateGame(Rank.Ten, Rank.Nine, Rank.Nine, Rank.Ten);

        game.Stand();

        game.Outcome.Should().Be(BlackjackOutcome.Push);
        game.CalculateTotalReturn().Should().Be(Bet);
    }

    [Fact]
    public void DoubleDown_DobraApostaRecebeUmaCartaEPara()
    {
        var game = CreateGame(Rank.Five, Rank.Three, Rank.Six, Rank.Three, Rank.Nine, Rank.King, Rank.Ace);

        game.DoubleDown();

        game.Bet.Should().Be(Bet * 2);
        game.Doubled.Should().BeTrue();
        game.Player.Cards.Should().HaveCount(3);
        game.Player.Value.Should().Be(20);
        game.Dealer.Value.Should().Be(17);
        game.Dealer.Cards.Should().HaveCount(4);
        game.Outcome.Should().Be(BlackjackOutcome.PlayerWin);
        game.CalculateTotalReturn().Should().Be(Bet * 4);
    }

    [Fact]
    public void DoubleDown_Estourando_PerdeApostaDobrada()
    {
        var game = CreateGame(Rank.Ten, Rank.Five, Rank.Nine, Rank.Six, Rank.King);

        game.DoubleDown();

        game.Bet.Should().Be(Bet * 2);
        game.Player.IsBust.Should().BeTrue();
        game.Outcome.Should().Be(BlackjackOutcome.DealerWin);
        game.CalculateTotalReturn().Should().Be(0);
    }

    [Fact]
    public void DoubleDown_DepoisDePedirCarta_LancaExcecao()
    {
        var game = CreateGame(Rank.Five, Rank.Three, Rank.Six, Rank.Three,
            Rank.King, Rank.Queen, Rank.Seven);

        game.Hit();
        game.Player.Cards.Should().HaveCount(3);

        var act = () => game.DoubleDown();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DoubleDown_DuasVezes_LancaExcecao()
    {
        var game = CreateGame(Rank.Five, Rank.Three, Rank.Six, Rank.Three,
            Rank.King, Rank.Ten, Rank.Ten);

        game.DoubleDown();

        var act = () => game.DoubleDown();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Acoes_MaoJaEncerrada_LancamExcecao()
    {
        var game = CreateGame(Rank.Ace, Rank.Queen, Rank.King, Rank.Three);

        game.Outcome.Should().NotBe(BlackjackOutcome.Pending);

        var hit = () => game.Hit();
        var stand = () => game.Stand();
        var doubleDown = () => game.DoubleDown();

        hit.Should().Throw<InvalidOperationException>();
        stand.Should().Throw<InvalidOperationException>();
        doubleDown.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ValorDaMao_AsSeAjustaParaNaoEstourar()
    {
        var game = CreateGame(Rank.Ace, Rank.Two, Rank.Ace, Rank.Three, Rank.Nine,
            Rank.Queen, Rank.King);

        game.Player.Value.Should().Be(12);
        game.Player.IsBust.Should().BeFalse();

        game.Hit();

        game.Player.Value.Should().Be(21);
        game.Dealer.IsBust.Should().BeTrue();
        game.Outcome.Should().Be(BlackjackOutcome.PlayerWin);
    }

    private static Card C(Suit suit, Rank rank) => new(suit, rank);

    private static Deck DeckOf(params Card[] cards) => new(cards);

    private static BlackjackGame CreateGame(params Rank[] ranks)
    {
        var suits = new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
        var cards = ranks.Select((rank, i) => C(suits[i % 4], rank));
        return new BlackjackGame(Bet, new Deck(cards));
    }
}
