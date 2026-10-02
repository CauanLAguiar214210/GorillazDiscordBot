using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Regras de leitura dos favoritos: origem com apelido opcional, busca por índice,
/// apelido, id ou origem, e escolha aleatória. Puro e síncrono — nenhuma I/O.
/// </summary>
public static class FavoriteSoundMatcher
{
    /// <summary>Separador usado entre origem e apelido: `origem = apelido`.</summary>
    public const string AliasSeparator = " = ";

    public const int MaxAliasLength = 32;
    public const int MaxFavorites = 100;

    public const string RandomKey = "aleatorio";

    /// <summary>
    /// Quebra `origem = apelido`. O separador considerado é o <b>último</b>,
    /// porque uma busca do YouTube pode legitimately conter " = ".
    /// </summary>
    public static bool TryParseInput(string? input, out string origin, out string? alias, out string? error)
    {
        origin = string.Empty;
        alias = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Informe a origem do áudio. Ex.: `macaco favs favoritar <link> = apelido`.";
            return false;
        }

        var trimmed = input.Trim();

        // Separador é ` = ` (espaços dos dois lados) para nunca quebrar uma query de
        // URL (`?si=abc`). Mas `origem =` e `= apelido` são erro de digitação claro.
        if (trimmed.StartsWith('='))
        {
            error = "A origem do áudio ficou vazia antes do `=`.";
            return false;
        }

        if (trimmed.EndsWith('='))
        {
            error = "O apelido ficou vazio depois do `=`. Use `origem = apelido`.";
            return false;
        }

        var cut = trimmed.LastIndexOf(AliasSeparator, StringComparison.Ordinal);

        if (cut < 0)
        {
            origin = trimmed;
            return true;
        }

        var rawOrigin = trimmed[..cut].Trim();
        var rawAlias = trimmed[(cut + AliasSeparator.Length)..].Trim();

        if (rawOrigin.Length == 0)
        {
            error = "A origem do áudio ficou vazia antes do `=`.";
            return false;
        }

        if (!TryNormalizeAlias(rawAlias, out alias, out error))
            return false;

        origin = rawOrigin;
        return true;
    }

    /// <summary>Valida e normaliza um apelido digitado isoladamente.</summary>
    public static bool TryNormalizeAlias(string? raw, out string? alias, out string? error)
    {
        alias = null;
        error = null;

        var trimmed = raw?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            error = "O apelido ficou vazio depois do `=`.";
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

    /// <summary>
    /// Localiza um favorito por índice (1-based), apelido, id ou própria origem.
    /// </summary>
    public static bool TryFind(
        IReadOnlyList<FavoriteSoundSettings> favorites,
        string? key,
        out FavoriteSoundSettings? favorite,
        out string? error)
    {
        favorite = null;
        error = null;

        if (favorites.Count == 0)
        {
            error = "Nenhum favorito ainda. Use `macaco favs favoritar <origem>` para criar o primeiro.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            error = "Informe o número, apelido ou id do favorito.";
            return false;
        }

        var trimmed = key.Trim();

        if (int.TryParse(trimmed, out var index))
        {
            if (index < 1 || index > favorites.Count)
            {
                error = $"Não existe o favorito **{index}** — a lista tem {favorites.Count} item(ns).";
                return false;
            }

            favorite = favorites[index - 1];
            return true;
        }

        favorite =
            favorites.FirstOrDefault(f => !string.IsNullOrEmpty(f.Alias) && f.Alias!.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            ?? favorites.FirstOrDefault(f => f.Id.ToString().StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
            ?? favorites.FirstOrDefault(f => f.AudioSource.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        if (favorite != null)
            return true;

        error = $"Nenhum favorito chamado **{trimmed}**. Use `macaco favs` para ver a lista.";
        return false;
    }

    public static bool IsRandomRequest(string? key)
        => !string.IsNullOrWhiteSpace(key)
           && key.Trim().Equals(RandomKey, StringComparison.OrdinalIgnoreCase);

    public static FavoriteSoundSettings? PickRandom(IReadOnlyList<FavoriteSoundSettings> favorites)
    {
        if (favorites.Count == 0)
            return null;

        return favorites[Random.Shared.Next(favorites.Count)];
    }

    /// <summary>Índice 1-based exibido ao usuário, para as mensagens de erro do Lavalink.</summary>
    public static int IndexOf(IReadOnlyList<FavoriteSoundSettings> favorites, FavoriteSoundSettings favorite)
        => favorites.ToList().FindIndex(f => ReferenceEquals(f, favorite)) + 1;

    public static string DisplayName(FavoriteSoundSettings favorite)
        => string.IsNullOrWhiteSpace(favorite.Alias) ? favorite.AudioSource : favorite.Alias!;

    public static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
