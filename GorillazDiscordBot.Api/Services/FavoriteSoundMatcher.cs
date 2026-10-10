using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Regras de leitura dos favoritos: normalização de apelido e busca por origem ou apelido.
/// Puro e síncrono — nenhuma I/O.
/// </summary>
public static class FavoriteSoundMatcher
{
    public const int MaxAliasLength = 32;
    public const int MaxFavorites = 100;

    /// <summary>Valida e normaliza um apelido digitado isoladamente.</summary>
    public static bool TryNormalizeAlias(string? raw, out string? alias, out string? error)
    {
        alias = null;
        error = null;

        var trimmed = raw?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            error = "O apelido ficou vazio.";
            return false;
        }

        if (trimmed.Length > MaxAliasLength)
        {
            error = $"Apelido muito longo (máximo {MaxAliasLength} caracteres).";
            return false;
        }

        if (trimmed.Any(char.IsControl))
        {
            error = "Apelido inválido: remova quebras de linha e caracteres de controle.";
            return false;
        }

        alias = trimmed;
        return true;
    }

    public static FavoriteSoundSettings? FindBySource(
        IReadOnlyList<FavoriteSoundSettings> favorites,
        string source)
        => favorites.FirstOrDefault(f =>
            f.AudioSource.Equals(source.Trim(), StringComparison.OrdinalIgnoreCase));

    public static FavoriteSoundSettings? FindByAlias(
        IReadOnlyList<FavoriteSoundSettings> favorites,
        string alias)
        => favorites.FirstOrDefault(f =>
            f.Alias != null && f.Alias.Equals(alias.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(FavoriteSoundSettings favorite)
        => string.IsNullOrWhiteSpace(favorite.Alias) ? favorite.AudioSource : favorite.Alias!;

    public static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
