namespace GorillazDiscordBot.Configuration;

/// <summary>
/// Limites do upload de áudio por anexo. O arquivo fica num volume compartilhado
/// com o Lavalink (<c>/opt/Lavalink/sounds/uploads</c> ↔ <c>/audio/uploads</c>),
/// então o bot valida antes de gravar: só o que o nó conseguir abrir vira favorito.
/// </summary>
public class AudioUploadOptions
{
    public const string DefaultPath = "/audio/uploads";
    public const int DefaultMaxMegabytes = 25;
    public const int DefaultMaxMinutes = 10;
    public const int DefaultRetentionDays = 30;

    /// <summary>Pasta compartilhada com o Lavalink, vista pelo bot.</summary>
    public string Path { get; set; } = DefaultPath;

    public int MaxMegabytes { get; set; } = DefaultMaxMegabytes;

    public int MaxMinutes { get; set; } = DefaultMaxMinutes;

    /// <summary>Uploads órfãos (não referenciados) são apagados depois deste prazo.</summary>
    public int RetentionDays { get; set; } = DefaultRetentionDays;

    public long MaxBytes => (long)Math.Clamp(MaxMegabytes, 1, 100) * 1024 * 1024;

    public TimeSpan MaxDuration => TimeSpan.FromMinutes(Math.Clamp(MaxMinutes, 1, 60));

    public TimeSpan Retention => TimeSpan.FromDays(Math.Clamp(RetentionDays, 1, 365));
}
