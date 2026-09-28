using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public static class AudioTrackResolver
{
    private const int MaxRelativeLength = 240;
    private static readonly char[] Separators = { '/', '\\' };

    public static AudioTrackResolveResult Resolve(string? origin, string? localAudioPath)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return IsValidResult(false, "Informe uma URL (http/https) ou um som local (`local:arquivo`).");

        var trimmed = origin.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return IsValidResult(true, trimmed);
        }

        if (trimmed.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
        {
            var relative = trimmed["local:".Length..].Trim();
            if (string.IsNullOrEmpty(relative))
                return IsValidResult(false, "Som local inválido. Use o formato `local:arquivo.mp3`.");

            if (relative[0] is '/' or '\\')
                return IsValidResult(false, "Som local deve ser relativo (sem `/` inicial). Ex.: `local:alarme.mp3`.");

            if (!IsSafeRelativePath(relative))
                return IsValidResult(false, "Caminho local inválido: use apenas letras, números, `-`, `_` e `.`, sem `..`.");

            var root = NormalizeLocalRoot(localAudioPath);
            return IsValidResult(true, $"local:{root}/{relative}");
        }

        return IsValidResult(false, "Origem deve ser uma URL (http/https) ou um som local (`local:arquivo`).");
    }

    public static bool IsSafeRelativePath(string relative)
    {
        if (string.IsNullOrEmpty(relative) || relative.Length > MaxRelativeLength)
            return false;

        var segments = relative.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
                return false;

            if (!segment.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
                return false;
        }

        return true;
    }

    public static bool IsRemoteUrl(string? identifier)
        => !string.IsNullOrWhiteSpace(identifier)
           && (identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public static string ToServerIdentifier(string identifier)
        => identifier.StartsWith("local:", StringComparison.OrdinalIgnoreCase)
            ? identifier["local:".Length..]
            : identifier;

    public static string NormalizeLocalRoot(string? localAudioPath)
    {
        if (string.IsNullOrWhiteSpace(localAudioPath))
            return LavalinkOptions.DefaultLocalAudioPath;

        var root = localAudioPath.Trim().TrimEnd('/', '\\');
        if (root.Length is 0 or > MaxRelativeLength)
            return LavalinkOptions.DefaultLocalAudioPath;

        if (root.Contains('/') || root.Contains('\\'))
            return LavalinkOptions.DefaultLocalAudioPath;

        return root.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            ? root
            : LavalinkOptions.DefaultLocalAudioPath;
    }

    private static AudioTrackResolveResult IsValidResult(bool isValid, string? identifierOrError)
        => isValid
            ? new AudioTrackResolveResult(true, identifierOrError, null)
            : new AudioTrackResolveResult(false, null, identifierOrError);
}