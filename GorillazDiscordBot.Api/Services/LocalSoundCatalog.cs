namespace GorillazDiscordBot.Services;

public static class LocalSoundCatalog
{
    public const string SoundsRelativePath = "Resources/Sounds";

    public const string TestSoundFileName = "oleodemacaco.mp3";

    public static string GetSoundsDirectory(string baseDirectory)
        => Path.Combine(baseDirectory, SoundsRelativePath);

    public static IReadOnlyList<LocalSound> List(string soundsDirectory)
    {
        if (!Directory.Exists(soundsDirectory))
            return Array.Empty<LocalSound>();

        return Directory.GetFiles(soundsDirectory, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(f => new LocalSound(Path.GetFileName(f), Path.GetRelativePath(soundsDirectory, f)))
            .OrderBy(s => s.FileName)
            .ToList();
    }

    public sealed record LocalSound(string FileName, string RelativePath);
}