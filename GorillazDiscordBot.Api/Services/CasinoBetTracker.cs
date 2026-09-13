using System.Collections.Concurrent;
using LuckyMonkey.Contracts.Enums;
using LuckyMonkey.Contracts.State;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Guarda metadados da partida em aberto de cada usuário: o valor da aposta e o último estado
/// renderizado (usado para pré-condições de UI, como dobrado duplo no blackjack) e o jogo
/// pendente (usado para auto-recuperação quando o serviço responde ActiveSession).
/// O jogo em si vive no microserviço de cassino.
/// </summary>
public sealed class CasinoBetTracker
{
    private readonly ConcurrentDictionary<ulong, ulong> _bets = new();
    private readonly ConcurrentDictionary<ulong, GameState> _states = new();
    private readonly ConcurrentDictionary<ulong, GameKind> _games = new();

    public void Set(ulong userId, ulong amount) => _bets[userId] = amount;

    public ulong Get(ulong userId) => _bets.TryGetValue(userId, out var amount) ? amount : 0;

    public void SetGame(ulong userId, GameKind game) => _games[userId] = game;

    public GameKind? GetGame(ulong userId) => _games.TryGetValue(userId, out var game) ? game : null;

    public void SetState(ulong userId, GameState state) => _states[userId] = state;

    public GameState? GetState(ulong userId) => _states.TryGetValue(userId, out var state) ? state : null;

    public void Remove(ulong userId)
    {
        _bets.TryRemove(userId, out _);
        _states.TryRemove(userId, out _);
        _games.TryRemove(userId, out _);
    }
}