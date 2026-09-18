namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class EntregadorGame
{
    public const ulong BasePerRound = 50;

    private static readonly string[] Destinations =
    {
        "\U0001F3E0 Casa", "\U0001F3E2 Escritório", "\U0001F3EB Escola",
        "\U0001F3EA Mercado", "\U0001F3E5 Clínica", "\U0001F3EC Shopping"
    };

    private static readonly (string Pkg, string Dest)[] Deliveries =
    {
        ("📦 Documentos", "\U0001F3E2 Escritório"),
        ("🍕 Pizza", "\U0001F3E0 Casa"),
        ("📚 Apostilas", "\U0001F3EB Escola"),
        ("🥫 Mantimentos", "\U0001F3EA Mercado"),
        ("💊 Receitas", "\U0001F3E5 Clínica"),
        ("🛍️ Sacolas", "\U0001F3EC Shopping"),
    };

    public static JobGameRound Step(Random rng)
    {
        var d = Deliveries[rng.Next(Deliveries.Length)];
        var options = Destinations
            .Where(x => x != d.Dest)
            .OrderBy(_ => rng.Next())
            .Take(3)
            .Append(d.Dest)
            .OrderBy(_ => rng.Next())
            .ToList();

        return new JobGameRound(
            $"🛵 **Entrega** — Leve {d.Pkg} até:",
            options,
            options.IndexOf(d.Dest));
    }
}