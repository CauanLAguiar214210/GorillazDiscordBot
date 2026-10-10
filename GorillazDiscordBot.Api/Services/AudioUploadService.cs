using GorillazDiscordBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Services;

public class AudioUploadService : IAudioUploadService
{
    /// <summary>Pasta do volume compartilhado, relativa à raiz local do Lavalink.</summary>
    public const string RelativeFolder = "uploads";

    private readonly IOptions<AudioUploadOptions> _options;
    private readonly ILogger<AudioUploadService> _logger;

    public AudioUploadService(
        IOptions<AudioUploadOptions> options,
        ILogger<AudioUploadService> logger)
    {
        _options = options;
        _logger = logger;
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

    /// <summary>
    /// Reconhece apenas os arquivos criados por este serviço: `<guid-N>.<ext>`
    /// (e o `.part` de upload interrompido legado).
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
