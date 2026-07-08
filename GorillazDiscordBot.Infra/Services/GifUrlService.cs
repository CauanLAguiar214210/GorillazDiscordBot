using System.Text.RegularExpressions;
using GorillazDiscordBot.Services.Interfaces;

namespace GorillazDiscordBot.Services;

public partial class GifUrlService : IGifUrlService
{
    private readonly HttpClient _httpClient;

    private static readonly HashSet<string> ImageExtensions = [".gif", ".png", ".jpg", ".jpeg", ".webp"];
    private static readonly HashSet<string> DirectHosts = ["c.tenor.com", "media.tenor.com"];

    public GifUrlService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetDirectUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL não pode estar vazia.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL inválida. Verifique o link e tente novamente.");

        if (IsDirectImageUrl(uri))
            return url;

        if (IsTenorUrl(uri))
            return await ExtractTenorGifUrlAsync(url);

        throw new InvalidOperationException(
            "URL inválida. Use uma URL direta de imagem (.gif, .png, .jpg) ou um link do Tenor.");
    }

    private static bool IsDirectImageUrl(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (DirectHosts.Contains(host)) return true;

        var path = uri.AbsolutePath.ToLowerInvariant();
        return ImageExtensions.Any(ext => path.EndsWith(ext));
    }

    private static bool IsTenorUrl(Uri uri)
    {
        return uri.Host.ToLowerInvariant().Contains("tenor.com");
    }

    private async Task<string> ExtractTenorGifUrlAsync(string url)
    {
        var html = await _httpClient.GetStringAsync(url);

        var ogMatch = OgImageRegex().Match(html);
        if (ogMatch.Success)
        {
            var gifUrl = ogMatch.Groups[1].Value;
            if (Uri.TryCreate(gifUrl, UriKind.Absolute, out var uri) && IsDirectImageUrl(uri))
                return gifUrl;
        }

        var fallbackMatch = TenorCdnRegex().Match(html);
        if (fallbackMatch.Success)
            return fallbackMatch.Value;

        throw new InvalidOperationException(
            "Não foi possível extrair a URL do GIF do Tenor. " +
            "Tente usar a URL direta da imagem (botão direito → 'Copiar endereço da imagem').");
    }

    [GeneratedRegex(@"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex OgImageRegex();

    [GeneratedRegex(@"https?://c\.tenor\.com/[^""'\s<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex TenorCdnRegex();
}
