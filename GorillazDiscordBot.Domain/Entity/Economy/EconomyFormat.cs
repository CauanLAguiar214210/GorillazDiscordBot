using System.Globalization;

namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class EconomyFormat
{
    private const ulong Milhao = 1_000_000;
    private const ulong Bilhao = 1_000_000_000;
    private const ulong Trilhao = 1_000_000_000_000;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Compact(ulong value)
    {
        if (value >= Trilhao)
            return $"{((decimal)value / Trilhao):0.#}T".Replace('.', ',');
        if (value >= Bilhao)
            return $"{((decimal)value / Bilhao):0.#}B".Replace('.', ',');
        if (value >= Milhao)
            return $"{((decimal)value / Milhao):0.#}M".Replace('.', ',');
        return value.ToString("N0", PtBr);
    }

    public static string Full(ulong value)
        => value.ToString("N0", PtBr);
}