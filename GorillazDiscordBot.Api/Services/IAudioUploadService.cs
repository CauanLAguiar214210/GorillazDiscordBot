using GorillazDiscordBot.Configuration;

namespace GorillazDiscordBot.Services;

public sealed record AudioUploadResult(
    bool Success,
    string? Origin,
    string? Error,
    string? TrackTitle = null,
    TimeSpan? Duration = null);

public interface IAudioUploadService
{
    /// <summary>
    /// Baixa o anexo, valida formato/tamanho/duração e grava no volume compartilhado.
    /// Em caso de qualquer falha nada é deixado para trás.
    /// </summary>
    Task<AudioUploadResult> UploadAsync(Uri source, string? fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apaga arquivos gerados por este serviço mais velhos que a retenção.
    /// A retenção vale mesmo para favoritos ainda referenciados: não existe como
    /// listar todas as guilds, então a idade máxima é o critério.
    /// Devolve quantos foram removidos.
    /// </summary>
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
