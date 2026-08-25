using FluentAssertions;
using GorillazDiscordBot.Domain.Games;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class GameSessionManagerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private GameSessionManager CreateManager() => new(() => _now, Timeout);

    [Fact]
    public void Add_GetActive_RetornaJogoAtivo()
    {
        var manager = CreateManager();
        var game = NewGame();

        manager.Add(1, game);

        manager.GetActive(1).Should().BeSameAs(game);
    }

    [Fact]
    public void GetActive_SemSessao_RetornaNull()
    {
        var manager = CreateManager();

        manager.GetActive(42).Should().BeNull();
    }

    [Fact]
    public void GetActive_AposTimeout_RetornaNull()
    {
        var manager = CreateManager();
        manager.Add(1, NewGame());

        _now += Timeout;

        manager.GetActive(1).Should().BeNull();
    }

    [Fact]
    public void Touch_RenovaAtividade_EvitaExpiracao()
    {
        var manager = CreateManager();
        var game = NewGame();
        manager.Add(1, game);

        _now += TimeSpan.FromMinutes(3);
        manager.Touch(1);

        _now += TimeSpan.FromMinutes(3);

        manager.GetActive(1).Should().BeSameAs(game);
    }

    [Fact]
    public void TakeExpired_ComSessaoExpirada_RetornaERemove()
    {
        var manager = CreateManager();
        var game = NewGame();
        manager.Add(1, game);

        _now += Timeout;

        manager.TakeExpired(1).Should().BeSameAs(game);
        manager.GetActive(1).Should().BeNull();
    }

    [Fact]
    public void TakeExpired_ComSessaoAtiva_RetornaNullEMantem()
    {
        var manager = CreateManager();
        var game = NewGame();
        manager.Add(1, game);

        _now += TimeSpan.FromMinutes(1);

        manager.TakeExpired(1).Should().BeNull();
        manager.GetActive(1).Should().BeSameAs(game);
    }

    [Fact]
    public void Remove_SessaoExistente_RetornaERemove()
    {
        var manager = CreateManager();
        var game = NewGame();
        manager.Add(1, game);

        manager.Remove(1).Should().BeSameAs(game);
        manager.GetActive(1).Should().BeNull();
    }

    private static BlackjackGame NewGame() => new(100, DeckOf());

    private static Deck DeckOf()
    {
        var suits = new[] { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
        return new Deck(suits.SelectMany(s => new[]
        {
            new Card(s, Rank.Ten), new Card(s, Rank.Six),
            new Card(s, Rank.Nine), new Card(s, Rank.Seven)
        }));
    }
}
