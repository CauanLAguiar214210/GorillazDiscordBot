using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class BankTaxService : IHostedService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<BankTaxService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _task;

    public BankTaxService(IUserRepository userRepository, ILogger<BankTaxService> logger)
    {
        _userRepository = userRepository;
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

            _logger.LogInformation("Bank tax agendado para {time} (daqui {delay})", nextMidnight, delay);

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
                var affected = await _userRepository.ApplyBankTaxAsync();
                _logger.LogInformation("Bank tax aplicado: {count} usuários afetados", affected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aplicar bank tax");
            }
        }
    }
}
