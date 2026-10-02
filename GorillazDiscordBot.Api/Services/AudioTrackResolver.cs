using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public static class AudioTrackResolver
{
    private const int MaxRelativeLength = 240;
    private const int MaxSearchLength = 200;
    private const int MaxInstantSlugLength = 80;
    private const string LocalPrefix = "local:";

    private static readonly char[] Separators = { '/', '\\' };

    private static readonly string[] SearchPrefixes = { "ytsearch:", "ytmsearch:" };

    private const string HelpMessage =
        "Origem deve ser um link do YouTube, uma busca (`ytsearch:termo` ou `!termo`), " +
        "um link de som instantâneo (Myinstants), uma URL direta de áudio (`http(s)://...`) " +
        "ou um som local (`local:arquivo`).";

    public static AudioTrackResolveResult Resolve(string? origin, string? localAudioPath)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return Error("Informe um link do YouTube, uma busca, um link de som instantâneo, uma URL de áudio ou `local:arquivo`.");

        var trimmed = origin.Trim();

        if (TryResolveSearch(trimmed, out var search, out var searchError))
            return searchError != null
                ? Error(searchError)
                : Valid(AudioOriginKind.YouTubeSearch, search);

        if (trimmed.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
            return ResolveLocal(trimmed, localAudioPath);

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (IsYouTubeUri(uri))
                return Valid(AudioOriginKind.YouTube, NormalizeYouTubeUri(uri, trimmed));

            if (TryGetInstantSlug(uri, out _))
                return Valid(AudioOriginKind.InstantButton, trimmed);

            return Valid(AudioOriginKind.DirectUrl, trimmed);
        }

        return Error(HelpMessage);
    }

    public static bool TryGetInstantSlug(string? origin, out string slug)
    {
        slug = string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri))
            return false;

        return TryGetInstantSlug(uri, out slug);
    }

    private static bool TryGetInstantSlug(Uri uri, out string slug)
    {
        slug = string.Empty;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (!IsInstantHost(uri.Host))
            return false;

        // myinstants.com/instant/<slug>-<id>, myinstants.com/<locale>/instant/<slug>-<id>
        // e myinstants.site/<slug>
        var path = uri.AbsolutePath.Trim('/');
        if (path.Length == 0)
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 1 && segments[0].Length == 2 && segments[0].All(char.IsAsciiLetter))
            segments = segments[1..];

        if (segments.Length == 0)
            return false;

        string last;

        if (segments[0].Equals("instant", StringComparison.OrdinalIgnoreCase))
        {
            if (segments.Length != 2)
                return false;

            last = StripInstantId(segments[1]);
        }
        else if (segments.Length == 1)
        {
            last = segments[0];
        }
        else
        {
            return false;
        }

        last = last.ToLowerInvariant();

        if (last.Length == 0 || last.Length > MaxInstantSlugLength)
            return false;

        if (!last.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            return false;

        if (last.StartsWith('-') || last.EndsWith('-'))
            return false;

        slug = last;
        return true;
    }

    public static bool IsYouTubeRequest(string? identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return false;

        var trimmed = identifier.Trim();

        if (SearchPrefixes.Any(p => trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (trimmed.StartsWith('!'))
            return true;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && IsYouTubeUri(uri);
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
        => identifier.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase)
            ? identifier[LocalPrefix.Length..]
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

    private static AudioTrackResolveResult ResolveLocal(string trimmed, string? localAudioPath)
    {
        var relative = trimmed[LocalPrefix.Length..].Trim();
        if (string.IsNullOrEmpty(relative))
            return Error("Som local inválido. Use o formato `local:arquivo.mp3`.");

        if (relative[0] is '/' or '\\')
            return Error("Som local deve ser relativo (sem `/` inicial). Ex.: `local:alarme.mp3`.");

        if (!IsSafeRelativePath(relative))
            return Error("Caminho local inválido: use apenas letras, números, `-`, `_` e `.`, sem `..`.");

        var root = NormalizeLocalRoot(localAudioPath);
        var normalizedRoot = root + "/";
        if (relative.Equals(root, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Valid(AudioOriginKind.Local, $"{LocalPrefix}{relative.Replace('\\', '/')}");
        }

        return Valid(AudioOriginKind.Local, $"{LocalPrefix}{root}/{relative}");
    }

    private static bool TryResolveSearch(string trimmed, out string identifier, out string? error)
    {
        identifier = string.Empty;
        error = null;

        string query;
        string prefix = "ytsearch:";

        if (trimmed.StartsWith('!'))
        {
            query = trimmed[1..].Trim();
        }
        else
        {
            var matched = SearchPrefixes.FirstOrDefault(p =>
                trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
                return false;

            prefix = matched.ToLowerInvariant();
            query = trimmed[matched.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            error = "Busca vazia. Use `ytsearch:termo` ou `!termo`.";
            return true;
        }

        if (query.Length > MaxSearchLength)
        {
            error = $"Busca muito longa (máximo {MaxSearchLength} caracteres).";
            return true;
        }

        identifier = $"{prefix}{query}";
        return true;
    }

    private static bool IsYouTubeUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var host = uri.Host.ToLowerInvariant();

        return host is "youtu.be" or "www.youtu.be"
            || host == "youtube.com" || host.EndsWith(".youtube.com")
            || host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com");
    }

    private static string NormalizeYouTubeUri(Uri uri, string original)
    {
        if (!HasQueryKey(uri.Query, "list")
            && !HasQueryKey(uri.Query, "start_radio")
            && !HasQueryKey(uri.Query, "pp"))
        {
            return original;
        }

        var videoId = GetQueryValue(uri.Query, "v");
        if (string.IsNullOrWhiteSpace(videoId))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2
                && (segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)
                    || segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase)))
            {
                videoId = segments[1];
            }
            else if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)
                     && segments.Length > 0)
            {
                videoId = segments[0];
            }
        }

        return string.IsNullOrWhiteSpace(videoId)
            ? original
            : $"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}";
    }

    private static bool HasQueryKey(string query, string key)
        => query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Any(part =>
            {
                var separator = part.IndexOf('=');
                var name = separator >= 0 ? part[..separator] : part;
                return name.Equals(key, StringComparison.OrdinalIgnoreCase);
            });

    private static string? GetQueryValue(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var name = separator >= 0 ? part[..separator] : part;
            if (!name.Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = separator >= 0 ? part[(separator + 1)..] : string.Empty;
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        return null;
    }

    private static bool IsInstantHost(string host)
    {
        host = host.ToLowerInvariant();

        return host is "myinstants.com" or "www.myinstants.com" or "myinstants.site" or "www.myinstants.site";
    }

    private static string StripInstantId(string segment)
    {
        var dash = segment.LastIndexOf('-');
        if (dash <= 0 || dash == segment.Length - 1)
            return segment;

        var suffix = segment[(dash + 1)..];
        return suffix.All(char.IsAsciiDigit) ? segment[..dash] : segment;
    }

    private static AudioTrackResolveResult Valid(AudioOriginKind kind, string identifier)
        => new(true, identifier, null, kind);

    private static AudioTrackResolveResult Error(string message)
        => new(false, null, message);
}
