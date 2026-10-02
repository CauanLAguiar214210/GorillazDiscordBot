using GorillazDiscordBot.Configuration;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Services;

public class AudioUploadService : IAudioUploadService
{
    public const string HttpClientName = "AudioUpload";

    /// <summary>Pasta do volume compartilhado, relativa à raiz local do Lavalink.</summary>
    public const string RelativeFolder = "uploads";

    private static readonly string[] AllowedExtensions = [".mp3", ".ogg", ".wav", ".flac", ".m4a"];

    private static readonly string[] AllowedHosts = ["cdn.discordapp.com", "media.discordapp.net", "cdn.discord.com"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAudioService _audio;
    private readonly IOptions<AudioUploadOptions> _options;
    private readonly IOptions<LavalinkOptions> _lavalinkOptions;
    private readonly ILogger<AudioUploadService> _logger;

    public AudioUploadService(
        IHttpClientFactory httpClientFactory,
        IAudioService audio,
        IOptions<AudioUploadOptions> options,
        IOptions<LavalinkOptions> lavalinkOptions,
        ILogger<AudioUploadService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _audio = audio;
        _options = options;
        _lavalinkOptions = lavalinkOptions;
        _logger = logger;
    }

    public async Task<AudioUploadResult> UploadAsync(
        Uri source,
        string? fileName,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedSource(source))
            return new AudioUploadResult(false, null, "Anexo inválido: só aceito arquivos do Discord por HTTPS.");

        if (!TryGetExtension(fileName, out var extension))
            return new AudioUploadResult(
                false,
                null,
                $"Formato não suportado. Aceito: {string.Join(", ", AllowedExtensions)}.");

        var options = _options.Value;
        var directory = Path.GetFullPath(options.Path);

        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Não consegui criar a pasta de uploads {directory}", directory);
            return new AudioUploadResult(false, null, "A pasta de uploads não está disponível no servidor.");
        }

        // Nome gerado: o nome enviado pelo usuário nunca vira caminho no disco.
        var fileNameGenerated = $"{Guid.NewGuid():N}{extension}";
        var destination = Path.Combine(directory, fileNameGenerated);
        var temporary = destination + ".part";

        try
        {
            var download = await DownloadAsync(source, options.MaxBytes, cancellationToken);
            if (download.Status != DownloadStatus.Ok)
            {
                return new AudioUploadResult(false, null, download.Status switch
                {
                    DownloadStatus.TooLarge => $"Arquivo maior que o limite de {options.MaxMegabytes} MB.",
                    DownloadStatus.Empty => "O anexo chegou vazio. Tente enviar o arquivo de novo.",
                    _ => "Não consegui baixar o anexo do Discord. Tente novamente.",
                });
            }

            await File.WriteAllBytesAsync(temporary, download.Bytes!, cancellationToken);

            // O Lavalink escolhe o codec pela extensão, então o arquivo precisa estar no nome
            // final antes de ser validado. Se a validação falhar, ele é removido abaixo.
            File.Move(temporary, destination, overwrite: true);

            var serverIdentifier = $"{AudioTrackResolver.NormalizeLocalRoot(_lavalinkOptions.Value.LocalAudioPath)}/{RelativeFolder}/{fileNameGenerated}";
            var track = await _audio.Tracks.LoadTrackAsync(serverIdentifier, default(TrackLoadOptions), default, cancellationToken);

            if (track == null)
            {
                Delete(destination);
                return new AudioUploadResult(
                    false,
                    null,
                    "O servidor de música não conseguiu abrir este arquivo — ele pode estar corrompido ou em formato não suportado.");
            }

            if (track.IsLiveStream)
            {
                Delete(destination);
                return new AudioUploadResult(false, null, "Anexo inválido: o Lavalink identificou isso como transmissão ao vivo.");
            }

            if (track.Duration > options.MaxDuration)
            {
                Delete(destination);
                return new AudioUploadResult(
                    false,
                    null,
                    $"Áudio longo demais ({FormatDuration(track.Duration)}). Limite: {options.MaxMinutes} min.");
            }

            return new AudioUploadResult(
                true,
                $"local:{RelativeFolder}/{fileNameGenerated}",
                null,
                ResolveTitle(track.Title, fileName),
                track.Duration);
        }
        catch (OperationCanceledException)
        {
            Delete(temporary);
            Delete(destination);
            throw;
        }
        catch (Exception ex)
        {
            Delete(temporary);
            Delete(destination);
            _logger.LogError(ex, "Falha ao processar upload de {source}", source);
            return new AudioUploadResult(false, null, "Não consegui processar este anexo. Tente novamente.");
        }
    }

    public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var directory = Path.GetFullPath(options.Path);

        if (!Directory.Exists(directory))
            return Task.FromResult(0);

        var limit = DateTime.UtcNow - options.Retention;
        var removed = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = Path.GetFileName(file);

                // Só o que o bot grava é removido; nada mais na pasta é tocado.
                if (!IsManagedFile(name))
                    continue;

                if (File.GetLastWriteTimeUtc(file) > limit)
                    continue;

                Delete(file);
                removed++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha na limpeza de uploads em {directory}", directory);
        }

        if (removed > 0)
            _logger.LogInformation("Limpeza de uploads: {count} arquivo(s) removido(s) em {directory}", removed, directory);

        return Task.FromResult(removed);
    }

    public static bool IsAllowedSource(Uri? source)
    {
        if (source == null || !source.IsAbsoluteUri)
            return false;

        if (!string.Equals(source.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        return AllowedHosts.Any(host => source.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetExtension(string? fileName, out string extension)
    {
        extension = string.Empty;

        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var candidate = Path.GetExtension(fileName.Trim()).ToLowerInvariant();
        if (candidate.Length == 0 || !AllowedExtensions.Contains(candidate))
            return false;

        extension = candidate;
        return true;
    }

    /// <summary>
    /// Sem tag ID3 o Lavalink devolve "Unknown title", que não ajuda ninguém a
    /// reconhecer o favorito: nesses casos fica o nome do anexo que o usuário enviou.
    /// </summary>
    private static string ResolveTitle(string? trackTitle, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(trackTitle) &&
            !trackTitle.Trim().Equals("Unknown title", StringComparison.OrdinalIgnoreCase))
        {
            return trackTitle;
        }

        return string.IsNullOrWhiteSpace(fileName) ? "áudio" : fileName;
    }

    public static string FormatDuration(TimeSpan duration)    {
        if (duration <= TimeSpan.Zero)
            return "0s";

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h{duration.Minutes:00}min"
            : duration.TotalMinutes >= 1
                ? $"{duration.Minutes}min{duration.Seconds:00}s"
                : $"{duration.Seconds}s";
    }

    private async Task<(DownloadStatus Status, byte[]? Bytes)> DownloadAsync(
        Uri source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Download do anexo falhou com status {status}", response.StatusCode);
                return (DownloadStatus.Failed, null);
            }

            // Content-Length é só uma dica: o limite real é aplicado na leitura.
            if (response.Content.Headers.ContentLength > maxBytes)
                return (DownloadStatus.TooLarge, null);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();

            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                    return (DownloadStatus.TooLarge, null);

                buffer.Write(chunk, 0, read);
            }

            return buffer.Length == 0
                ? (DownloadStatus.Empty, null)
                : (DownloadStatus.Ok, buffer.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha de rede ao baixar o anexo de {source}", source);
            return (DownloadStatus.Failed, null);
        }
    }

    private enum DownloadStatus
    {
        Ok,
        TooLarge,
        Empty,
        Failed,
    }

    /// <summary>
    /// Reconhece apenas os arquivos criados por este serviço: `<guid-N>.<ext>`
    /// (e o `.part` do download interrompido).
    /// </summary>
    private static bool IsManagedFile(string fileName)
    {
        const string partial = ".part";

        if (fileName.EndsWith(partial, StringComparison.OrdinalIgnoreCase))
            return HasGeneratedName(fileName[..^partial.Length]);

        return HasGeneratedName(fileName);
    }

    private static bool HasGeneratedName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length == 0)
            return false;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        return stem.Length == 32 && Guid.TryParseExact(stem, "N", out _);
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception)
        {
            // Arquivo preso pelo Lavalink: a próxima limpeza tenta de novo.
        }
    }
}
