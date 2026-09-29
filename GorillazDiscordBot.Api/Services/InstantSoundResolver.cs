using System.Collections.Concurrent;
using GorillazDiscordBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Services;

public sealed record InstantSoundResolveResult(bool Success, string? AudioUrl, string? Error);

public interface IInstantSoundResolver
{
    Task<InstantSoundResolveResult> ResolveAsync(string origin, CancellationToken cancellationToken = default);
}

public class InstantSoundResolver : IInstantSoundResolver
{
    public const string HttpClientName = "instant-sound";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LavalinkOptions> _options;
    private readonly ILogger<InstantSoundResolver> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public InstantSoundResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<LavalinkOptions> options,
        ILogger<InstantSoundResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<InstantSoundResolveResult> ResolveAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (!AudioTrackResolver.TryGetInstantSlug(origin, out var slug))
            return new InstantSoundResolveResult(false, null, "Link de som instantâneo inválido. Use o link da página do som (ex.: `https://www.myinstants.com/instant/nome-do-som-1234`).");

        var candidate = BuildAudioUrl(slug);

        if (_cache.TryGetValue(candidate, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return new InstantSoundResolveResult(true, cached.AudioUrl, null);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, candidate);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _cache[candidate] = new CacheEntry(candidate, DateTimeOffset.UtcNow.Add(CacheTtl));
                return new InstantSoundResolveResult(true, candidate, null);
            }

            _logger.LogInformation(
                "Som instantâneo {slug} indisponível no espelho ({status}): {origin}",
                slug,
                (int)response.StatusCode,
                origin);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao resolver som instantâneo {slug}", slug);
        }

        return new InstantSoundResolveResult(
            false,
            null,
            $"Não consegui baixar o som instantâneo `{slug}`. Esse som não está disponível no espelho — " +
            "tente o link direto do MP3 ou coloque o arquivo em `Resources/Sounds/` e use `local:`.");
    }

    private string BuildAudioUrl(string slug)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.Value.InstantMirrorBaseUrl)
            ? LavalinkOptions.DefaultInstantMirrorBaseUrl
            : _options.Value.InstantMirrorBaseUrl.TrimEnd('/');

        return $"{baseUrl}/media/sound/{slug}.mp3";
    }

    private sealed record CacheEntry(string AudioUrl, DateTimeOffset ExpiresAt);
}
