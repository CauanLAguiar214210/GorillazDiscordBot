namespace GorillazDiscordBot.Domain.Entity.Games;

/// <summary>Caça-níqueis com tema da selva (macacos, bananas, etc.).</summary>
public sealed class SlotGame
{
    public static readonly IReadOnlyList<string> Symbols = new[]
    {
        "🐵", // macaco
        "🍌", // banana
        "🦍", // gorila
        "🌴", // palmeira
        "🥥"  // coco
    };

    public IReadOnlyList<string> Reels { get; }

    public SlotGame()
    {
        Reels = new[] { Draw(), Draw(), Draw() };
    }

    private static string Draw() => Symbols[Random.Shared.Next(Symbols.Count)];

    /// <summary>Multiplicador do valor total devolvido ao jogador (inclui a aposta). 0 = perda.</summary>
    public int Multiplier
    {
        get
        {
            if (Reels[0] == Reels[1] && Reels[1] == Reels[2])
                return 5;

            if (Reels[0] == Reels[1] || Reels[1] == Reels[2] || Reels[0] == Reels[2])
                return 2;

            return 0;
        }
    }

    public bool IsWin => Multiplier > 0;
}
