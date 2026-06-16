using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services.Interfaces;

public interface IFOneService
{
    Task<List<DriverStanding>?> ObterClassificacaoPilotosAsync();
}
