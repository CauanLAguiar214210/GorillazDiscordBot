namespace GorillazDiscordBot.Services.Interfaces;

public interface ICotacaoService
{
    Task<Dictionary<string, decimal?>> ObterCotacoesAsync(params string[] codigosMoeda);
}
