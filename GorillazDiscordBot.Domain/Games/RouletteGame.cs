namespace GorillazDiscordBot.Domain.Games;

public enum RouletteColor
{
    Red,
    Black,
    Green
}

public enum RouletteBetKind
{
    Number,
    Color,
    EvenOdd,
    Half
}

public sealed class RouletteBet
{
    public RouletteBetKind Kind { get; }
    public int? Number { get; }
    public RouletteColor? Color { get; }
    public bool? Even { get; }
    public bool? Low { get; }

    private RouletteBet(RouletteBetKind kind, int? number, RouletteColor? color, bool? even, bool? low)
    {
        Kind = kind;
        Number = number;
        Color = color;
        Even = even;
        Low = low;
    }

    public static RouletteBet OnNumber(int number) => new(RouletteBetKind.Number, number, null, null, null);
    public static RouletteBet OnColor(RouletteColor color) => new(RouletteBetKind.Color, null, color, null, null);
    public static RouletteBet OnEvenOdd(bool even) => new(RouletteBetKind.EvenOdd, null, null, even, null);
    public static RouletteBet OnHalf(bool low) => new(RouletteBetKind.Half, null, null, null, low);

    public string Describe() => Kind switch
    {
        RouletteBetKind.Number => $"Número {Number}",
        RouletteBetKind.Color => Color == RouletteColor.Red ? "Vermelho" : "Preto",
        RouletteBetKind.EvenOdd => Even == true ? "Par" : "Ímpar",
        RouletteBetKind.Half => Low == true ? "Baixo (1-18)" : "Alto (19-36)",
        _ => "?"
    };
}

public sealed class RouletteGame
{
    private static readonly HashSet<int> RedNumbers = new()
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    public int Result { get; }

    public RouletteGame(int? forcedResult = null)
    {
        Result = forcedResult ?? Random.Shared.Next(0, 37);
    }

    public RouletteColor Color => Result == 0
        ? RouletteColor.Green
        : RedNumbers.Contains(Result) ? RouletteColor.Red : RouletteColor.Black;

    public bool IsEven => Result != 0 && Result % 2 == 0;
    public bool IsLow => Result is >= 1 and <= 18;

    /// <summary>Retorna o multiplicador do valor total devolvido ao jogador (inclui a aposta). 0 = perda.</summary>
    public int Evaluate(RouletteBet bet) => bet.Kind switch
    {
        RouletteBetKind.Number => bet.Number == Result ? 36 : 0,
        RouletteBetKind.Color => bet.Color == Color && Color != RouletteColor.Green ? 2 : 0,
        RouletteBetKind.EvenOdd => bet.Even == IsEven && Result != 0 ? 2 : 0,
        RouletteBetKind.Half => bet.Low == IsLow && Result != 0 ? 2 : 0,
        _ => 0
    };
}
