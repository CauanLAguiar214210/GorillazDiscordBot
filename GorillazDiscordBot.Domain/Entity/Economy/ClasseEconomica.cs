namespace GorillazDiscordBot.Domain.Entity.Economy;

public sealed record ClasseEconomica(string Title, string Emoji, ulong MinPatrimonio)
{
    public static IReadOnlyList<ClasseEconomica> All { get; } = new[]
    {
        new ClasseEconomica("Miserável", "\U0001F573",                0),
        new ClasseEconomica("Quebrado", "\U0001F4B8",             5_000),        
        new ClasseEconomica("Proletariado", "\U0001F95A",        10_000),
        new ClasseEconomica("Emergente", "\U0001F680",          100_000),
        new ClasseEconomica("Rico", "\U0001F48E",             1_000_000),
        new ClasseEconomica("Milionário", "\U0001F3E6",     100_000_000),
        new ClasseEconomica("Magnata", "\U0001F451",      1_000_000_000),
        new ClasseEconomica("Oligarca", "\U0001F451", 1_000_000_000_000),
    };

    public static ClasseEconomica Find(ulong patrimonio)
    {
        ClasseEconomica best = All[0];
        foreach (var classe in All)
        {
            if (patrimonio >= classe.MinPatrimonio)
                best = classe;
            else
                break;
        }
        return best;
    }
}