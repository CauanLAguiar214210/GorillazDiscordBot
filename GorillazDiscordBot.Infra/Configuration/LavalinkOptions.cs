namespace GorillazDiscordBot.Configuration;

public class LavalinkOptions
{
    public const string DefaultRestUri = "http://localhost:2333";
    public const string DefaultWebSocketUri = "ws://localhost:2333";
    public const string DefaultPassphrase = "youshallnotpass";
    public const string DefaultLocalAudioPath = "sounds";
    public const string DefaultInstantMirrorBaseUrl = "https://myinstants.site";
    public const int DefaultPlaybackStartTimeoutSeconds = 15;

    public string RestUri { get; set; } = DefaultRestUri;
    public string WebSocketUri { get; set; } = DefaultWebSocketUri;
    public string Passphrase { get; set; } = DefaultPassphrase;
    public string LocalAudioPath { get; set; } = DefaultLocalAudioPath;

    public string InstantMirrorBaseUrl { get; set; } = DefaultInstantMirrorBaseUrl;

    /// <summary>
    /// Tempo que o bot espera o Lavalink confirmar (TrackStarted) que a faixa começou.
    /// Sem isso, um stream que morre na abertura passava por "tocando" sem erro.
    /// </summary>
    public int PlaybackStartTimeoutSeconds { get; set; } = DefaultPlaybackStartTimeoutSeconds;
}