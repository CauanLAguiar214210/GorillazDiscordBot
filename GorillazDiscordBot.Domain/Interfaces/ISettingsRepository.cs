namespace GorillazDiscordBot.Domain.Interfaces;

public interface ISettingsRepository<T> where T : class, IGuildSettings, new()
{
    Task<T> GetAsync(ulong guildId);

    /// <summary>
    /// Persiste as configurações. Retorna <c>false</c> se a gravação falhar
    /// (o erro é logado) — o chamador não deve reportar sucesso sem checar.
    /// </summary>
    Task<bool> SaveAsync(T settings);

    /// <summary>
    /// Executa uma mutação sob lock da guilda (get → mutate → save) para serializar
    /// escritas concorrentes. A mutação retorna <c>false</c> para recusar a gravação
    /// (nesse caso o retorno é <c>true</c>: nada falhou). Só retorna <c>false</c>
    /// quando a persistência falha.
    /// </summary>
    Task<bool> MutateAsync(ulong guildId, Func<T, bool> mutate);

    Task ResetAsync(ulong guildId);
}
