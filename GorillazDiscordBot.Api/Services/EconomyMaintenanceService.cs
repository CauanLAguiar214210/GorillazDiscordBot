using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class EconomyMaintenanceService : IHostedService
{
    private readonly IEconomyRepository _economyRepository;
    private readonly ILogger<EconomyMaintenanceService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    public EconomyMaintenanceService(IEconomyRepository economyRepository, ILogger<EconomyMaintenanceService> logger)
    {
        _economyRepository = economyRepository;
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
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;

            _logger.LogInformation("Manutenção econômica agendada para {time} (daqui {delay})", nextMidnight, delay);

            try
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var affected = await _economyRepository.ApplyDailyMaintenanceAsync();
                _logger.LogInformation("Manutenção econômica aplicada: {count} usuários afetados", affected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aplicar manutenção econômica");
            }
        }
    }
}