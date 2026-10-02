namespace GorillazDiscordBot.Services;

public static class LocalSoundCatalog
{
    private static readonly string[] AudioExtensions =
        [".mp3", ".ogg", ".wav", ".flac", ".m4a", ".aac", ".opus", ".webm", ".aif", ".aiff", ".wma"];

    public const string SoundsRelativePath = "Resources/Sounds";

    public const string TestSoundFileName = "oleodemacaco.mp3";

    public static string GetSoundsDirectory(string baseDirectory)
        => Path.Combine(baseDirectory, SoundsRelativePath);

    public static IReadOnlyList<LocalSound> List(string soundsDirectory, string serverPathPrefix = "")
    {
        if (!Directory.Exists(soundsDirectory))
            return Array.Empty<LocalSound>();

        return Directory.GetFiles(soundsDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f =>
            {
                var relativePath = Path.GetRelativePath(soundsDirectory, f);
                if (!string.IsNullOrWhiteSpace(serverPathPrefix))
                    relativePath = Path.Combine(serverPathPrefix, relativePath);

                return new LocalSound(Path.GetFileName(f), relativePath);
            })
            .OrderBy(s => s.FileName)
            .ToList();
    }

    public sealed record LocalSound(string FileName, string RelativePath);
}
