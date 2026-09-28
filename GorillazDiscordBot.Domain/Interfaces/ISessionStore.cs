using System.Diagnostics.CodeAnalysis;

namespace GorillazDiscordBot.Domain.Interfaces;

/// <summary>
/// Contrato de armazenamento de sessões efêmeras (gameplay em andamento no bot).
/// Hoje implementado em memória (<c>ConcurrentDictionary</c>); no futuro pode ser
/// Redis ou um serviço de sessões — sem alterar a lógica dos serviços de sessão.
/// </summary>
public interface ISessionStore<TKey, TSession> where TKey : notnull
{
    bool TryGet(TKey key, [MaybeNullWhen(false)] out TSession session);

    /// <summary>Retorna a sessão existente para a chave ou adiciona <paramref name="value"/> se não houver.</summary>
    TSession GetOrAdd(TKey key, TSession value);

    /// <summary>Substitui a sessão da chave pelo <paramref name="value"/>, retornando-o.</summary>
    TSession AddOrReplace(TKey key, TSession value);

    bool TryRemove(TKey key);
}