using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public interface IAudioUploadService
{
    /// <summary>
    /// Apaga arquivos gerados por este serviço mais velhos que a retenção.
    /// A retenção vale mesmo para favoritos ainda referenciados: não existe como
    /// listar todas as guilds, então a idade máxima é o critério.
    /// Devolve quantos foram removidos.
    /// </summary>
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
