using System.Text.RegularExpressions;

namespace GorillazDiscordBot.Utils;

public static class MyInstantsResolver
{
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "myinstants.com", "www.myinstants.com"
    };

    private static readonly Regex PagePathRegex =
        new(@"^/(?<lang>[a-z]{2}/)?instant/[^/]+/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MetaTagRegex =
        new(@"<meta\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AudioButtonRegex =
        new(@"data-url=""(?<url>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsPageUrl(Uri uri)
        => Hosts.Contains(uri.Host) && PagePathRegex.IsMatch(uri.AbsolutePath);

    public static Uri? ExtractAudioUrl(string html, Uri pageUrl)
    {
        foreach (Match meta in MetaTagRegex.Matches(html))
        {
            if (!meta.Value.Contains("og:audio", StringComparison.OrdinalIgnoreCase))
                continue;

            var content = GetAttributeValue(meta.Value, "content");
            if (content is not null && TryResolve(content, pageUrl, out var resolved))
                return resolved;
        }

        var button = AudioButtonRegex.Match(html);
        if (button.Success && TryResolve(button.Groups["url"].Value, pageUrl, out var fromButton))
            return fromButton;

        return null;
    }

    private static string? GetAttributeValue(string tag, string attributeName)
    {
        var match = Regex.Match(tag, $@"\b{attributeName}=""(?<value>[^""]*)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static bool TryResolve(string raw, Uri baseUri, out Uri resolved)
    {
        resolved = baseUri;
        if (!Uri.TryCreate(raw, UriKind.RelativeOrAbsolute, out var candidate))
            return false;
        if (!Uri.TryCreate(baseUri, candidate, out var absolute))
            return false;
        if (absolute.Scheme != Uri.UriSchemeHttp && absolute.Scheme != Uri.UriSchemeHttps)
            return false;

        resolved = absolute;
        return true;
    }
}
