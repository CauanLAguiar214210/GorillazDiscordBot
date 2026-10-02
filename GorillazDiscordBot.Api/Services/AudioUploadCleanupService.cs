using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Apaga uploads antigos periodicamente. Roda logo no boot (para recuperar
/// buffers de downloads interrompidos) e depois a cada 6h.
/// </summary>
public class AudioUploadCleanupService : IHostedService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IAudioUploadService _uploads;
    private readonly ILogger<AudioUploadCleanupService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    public AudioUploadCleanupService(IAudioUploadService uploads, ILogger<AudioUploadCleanupService> logger)
    {
        _uploads = uploads;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _task = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();
        if (_task != null) await _task;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _uploads.CleanupAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha na limpeza periódica de uploads");
            }

            try
            {
                await Task.Delay(Interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
