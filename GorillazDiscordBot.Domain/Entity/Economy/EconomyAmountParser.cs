using System.Globalization;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class EconomyAmountParser
{
    private const decimal Mil = 1_000m;
    private const decimal Milhao = 1_000_000m;
    private const decimal Bilhao = 1_000_000_000m;
    private const decimal Trilhao = 1_000_000_000_000m;

    private static readonly (string Suffix, decimal Multiplier)[] Suffixes =
    {
        ("trilhoes", Trilhao),
        ("trilhões", Trilhao),
        ("trilhao", Trilhao),
        ("trilhão", Trilhao),
        ("bilhoes", Bilhao),
        ("bilhões", Bilhao),
        ("bilhao", Bilhao),
        ("bilhão", Bilhao),
        ("milhoes", Milhao),
        ("milhões", Milhao),
        ("milhao", Milhao),
        ("milhão", Milhao),
        ("mil", Mil),
        ("t", Trilhao),
        ("b", Bilhao),
        ("m", Milhao),
        ("k", Mil),
    };

    public static bool TryParse(string input, out ulong amount, out string? error)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "⚠️ Informe um valor. Exemplos: `100`, `1k`, `1,5M`, `1B`.";
            return false;
        }

        var text = input.Trim();

        if (text.StartsWith('-'))
        {
            error = "⚠️ O valor deve ser positivo.";
            return false;
        }

        var (suffix, suffixMultiplier) = MatchSuffix(text);

        string numberPart;
        if (suffix != null)
        {
            numberPart = text[..^suffix.Length].Trim();
        }
        else if (HasTrailingLetter(text))
        {
            error = "⚠️ Sufixo inválido. Use `k`/`mil`, `M`/`milhão`, `B`/`bilhão` ou `T`/`trilhão`.";
            return false;
        }
        else
        {
            numberPart = text;
        }

        if (!TryParseNumberPart(numberPart, suffix != null, out var value))
        {
            error = "⚠️ Informe um valor numérico válido. Exemplos: `100`, `1k`, `1,5M`, `1B`.";
            return false;
        }

        var multiplier = suffixMultiplier ?? 1m;

        if (value > (decimal)ulong.MaxValue / multiplier)
        {
            error = "⚠️ Valor muito grande.";
            return false;
        }

        var result = value * multiplier;

        if (result < 1)
        {
            error = "⚠️ O valor deve ser positivo.";
            return false;
        }

        amount = (ulong)Math.Ceiling(result);
        error = null;
        return true;
    }

    private static (string? Suffix, decimal? Multiplier) MatchSuffix(string text)
    {
        foreach (var (suffix, multiplier) in Suffixes)
        {
            if (text.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return (suffix, multiplier);
        }

        return (null, null);
    }

    private static bool HasTrailingLetter(string text)
        => text.Length > 0 && char.IsLetter(text[^1]);

    private static bool TryParseNumberPart(string part, bool allowDecimal, out decimal value)
    {
        value = 0;

        if (part.Length == 0)
            return false;

        if (part.Contains(','))
        {
            if (!allowDecimal)
                return false;
            part = part.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (part.Contains('.'))
        {
            if (IsThousandsGrouped(part))
                part = part.Replace(".", string.Empty);
            else if (!allowDecimal)
                return false;
        }

        return decimal.TryParse(part, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsThousandsGrouped(string text)
    {
        var segments = text.Split('.');
        if (segments.Length < 2)
            return false;

        if (segments[0].Length is < 1 or > 3 || !segments[0].All(char.IsDigit))
            return false;

        foreach (var segment in segments.Skip(1))
        {
            if (segment.Length != 3 || !segment.All(char.IsDigit))
                return false;
        }

        return true;
    }
}