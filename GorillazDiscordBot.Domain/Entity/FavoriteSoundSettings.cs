namespace GorillazDiscordBot.Entity;

/// <summary>
/// Som favoritado da guilda. Guarda a <b>origem</b> (mesmo formato de
/// <see cref="ScheduledSoundSettings.AudioSource"/>), nunca os bytes do arquivo,
/// para sobreviver a restart e permitir migrar o espelho depois.
/// </summary>
public class FavoriteSoundSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Origem do áudio (link, busca, Myinstants, `local:...`).</summary>
    public string AudioSource { get; set; } = string.Empty;

    /// <summary>Apelido curto digitado pelo usuário (opcional).</summary>
    public string? Alias { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int TimesPlayed { get; set; }
}
