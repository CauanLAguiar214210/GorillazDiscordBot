namespace GorillazDiscordBot.Configuration;

public class LavalinkOptions
{
    public const string DefaultRestUri = "http://localhost:2333";
    public const string DefaultWebSocketUri = "ws://localhost:2333";
    public const string DefaultPassphrase = "youshallnotpass";
    public const string DefaultLocalAudioPath = "sounds";

    public string RestUri { get; set; } = DefaultRestUri;
    public string WebSocketUri { get; set; } = DefaultWebSocketUri;
    public string Passphrase { get; set; } = DefaultPassphrase;
    public string LocalAudioPath { get; set; } = DefaultLocalAudioPath;
}