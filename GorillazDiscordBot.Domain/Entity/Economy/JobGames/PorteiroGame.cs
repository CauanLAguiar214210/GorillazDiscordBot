namespace GorillazDiscordBot.Domain.Entity.Economy;

public sealed record PorteiroCadastro(string Name, string Apartment, string Badge, string Role);

public sealed record PorteiroCheck(PorteiroCadastro Cadastro, PorteiroCadastro Arrival, string Rule)
{
    public bool IsMatch => Arrival == Cadastro;
    public bool IsDoppelganger => !IsMatch;
}

public enum PorteiroMutation
{
    Name,
    Apartment,
    Badge,
    BadgeDigit
}

public static class PorteiroGame
{
    public const ulong BasePerRound = 70;
    public const ulong DoppelgangerBonus = 40;
    public const double IdentityChance = 0.75;
    public const double DoppelgangerChance = 0.55;

    private const int LetInIndex = 0;
    private const int BarIndex = 1;
    private const string IdentityRule = "Confira crachá e apartamento: qualquer divergência é impostor.";

    private static readonly string[] Decisions = { "\u2705 Deixar entrar", "\u26D4 Barrar" };

    private static readonly PorteiroCadastro[] Credentials =
    {
        new("R. Silva", "302", "#A12", "morador"),
        new("C. Lima", "404", "#B07", "moradora"),
        new("M. Souza", "105", "#C21", "morador"),
        new("A. Pereira", "708", "#D55", "morador"),
        new("J. Alves", "203", "#E09", "moradora"),
        new("Entregador Pizza", "302", "#P88", "entregador autorizado"),
        new("Técnico de Internet", "bloco B", "#T41", "técnico com OS"),
    };

    private static readonly (string Visitor, string Rule, bool LetIn)[] RuleScenarios =
    {
        ("Morador **R. Silva** (ap. 302) sem crachá", "Morador sem crachá deve subir normalmente com identificação.", true),
        ("Entregador de **pizza** com autorização", "Entregadores com autorização podem subir.", true),
        ("Visitante **desconhecido** sem documento", "Visitante sem documento e sem morador deve ser barrado.", false),
        ("Técnico de **internet** identificado com OS", "Técnicos com ordem de serviço podem entrar.", true),
        ("Coletor com crachá de **outra empresa**", "Apenas funcionários credenciados deste prédio entram.", false),
        ("Amigo do morador do **404** sem aviso prévio", "Visitas exigem aviso prévio do morador.", false),
        ("Moradora **C. Lima** com filho menor", "Moradores e dependentes passam normalmente.", true),
        ("Propagandista **não autorizado**", "Proibida a entrada de propagandistas não autorizados.", false),
    };

    public static IReadOnlyList<PorteiroMutation> MutationsFor(int roundIndex) => roundIndex switch
    {
        <= 1 => new[] { PorteiroMutation.Name, PorteiroMutation.Apartment },
        <= 3 => new[] { PorteiroMutation.Apartment, PorteiroMutation.Badge },
        _ => new[] { PorteiroMutation.BadgeDigit }
    };

    public static PorteiroCheck Build(int roundIndex, Random rng)
    {
        var cadastro = Credentials[rng.Next(Credentials.Length)];
        var arrival = rng.NextDouble() < DoppelgangerChance
            ? Mutate(cadastro, roundIndex, rng)
            : cadastro;

        return new PorteiroCheck(cadastro, arrival, IdentityRule);
    }

    public static JobGameRound ToRound(PorteiroCheck check)
    {
        var prompt =
            "\U0001F6AA **Portaria** — Verifique a credencial\n\n" +
            $"\U0001F4CB **Cadastro**\n{Describe(check.Cadastro)}\n\n" +
            $"\U0001F6AA **Chegou**\n{Describe(check.Arrival)}\n\n" +
            $"\U0001F4CF Regra: {check.Rule}";

        return check.IsDoppelganger
            ? new JobGameRound(prompt, Decisions, BarIndex, "Impostor detectado", DoppelgangerBonus)
            : new JobGameRound(prompt, Decisions, LetInIndex);
    }

    public static JobGameRound Step(int roundIndex, Random rng)
    {
        if (rng.NextDouble() < IdentityChance)
            return ToRound(Build(roundIndex, rng));

        var sc = RuleScenarios[rng.Next(RuleScenarios.Length)];
        return new JobGameRound(
            $"\U0001F6AA **Portaria** — Chegou: {sc.Visitor}\n\U0001F4CB Regra: {sc.Rule}",
            Decisions,
            sc.LetIn ? LetInIndex : BarIndex);
    }

    private static string Describe(PorteiroCadastro c)
        => $"Nome: {c.Name}\nApartamento: {c.Apartment}\nCrachá: {c.Badge}\nTipo: {c.Role}";

    private static PorteiroCadastro Mutate(PorteiroCadastro cadastro, int roundIndex, Random rng)
    {
        var mutations = MutationsFor(roundIndex);
        var mutation = mutations[rng.Next(mutations.Count)];

        return mutation switch
        {
            PorteiroMutation.Name => cadastro with { Name = DifferentValue(cadastro.Name, c => c.Name, rng) },
            PorteiroMutation.Apartment => cadastro with { Apartment = DifferentValue(cadastro.Apartment, c => c.Apartment, rng) },
            PorteiroMutation.Badge => cadastro with { Badge = DifferentValue(cadastro.Badge, c => c.Badge, rng) },
            PorteiroMutation.BadgeDigit => cadastro with { Badge = ShiftLastDigit(cadastro.Badge, rng) },
            _ => cadastro
        };
    }

    private static string DifferentValue(string current, Func<PorteiroCadastro, string> selector, Random rng)
    {
        var candidates = Credentials.Select(selector).Where(v => v != current).ToArray();
        return candidates[rng.Next(candidates.Length)];
    }

    private static string ShiftLastDigit(string badge, Random rng)
    {
        var delta = rng.Next(1, 10);
        var last = badge[^1];

        if (!char.IsDigit(last))
            return badge + delta;

        var shifted = (char)('0' + ((last - '0' + delta) % 10));
        return badge[..^1] + shifted;
    }
}
