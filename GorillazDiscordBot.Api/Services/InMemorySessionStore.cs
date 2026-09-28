using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Armazenamento de sessões em memória (por processo). Thread-safe.
/// </summary>
public sealed class InMemorySessionStore<TKey, TSession> : ISessionStore<TKey, TSession> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TSession> _items = new();

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TSession session)
        => _items.TryGetValue(key, out session);

    public TSession GetOrAdd(TKey key, TSession value)
        => _items.GetOrAdd(key, value);

    public TSession AddOrReplace(TKey key, TSession value)
        => _items.AddOrUpdate(key, value, (_, _) => value);

    public bool TryRemove(TKey key)
        => _items.TryRemove(key, out _);
}