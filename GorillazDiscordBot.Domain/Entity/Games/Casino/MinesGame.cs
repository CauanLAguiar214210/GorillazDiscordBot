namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class MinesGame
{
    private readonly HashSet<int> _revealed = new();

    public ulong Bet { get; }

    public int MinesCount { get; }

    public int GridSize { get; }

    public IReadOnlySet<int> Mines { get; }

    public IReadOnlySet<int> Revealed => _revealed;

    public int RevealedCount => _revealed.Count;

    public bool HasBoom { get; private set; }

    public bool HasCashedOut { get; private set; }

    public bool IsFinished => HasBoom || HasCashedOut;

    public double CurrentMultiplier { get; private set; } = 1.0;

    public MinesGame(
        ulong bet, int minesCount, Func<int>? minePicker = null, int gridSize = CasinoRules.MinesGridSize)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (minesCount <= 0 || minesCount >= gridSize)
            throw new ArgumentOutOfRangeException(nameof(minesCount), "A quantidade de minas deve estar entre 1 e o tamanho da grade menos 1.");

        Bet = bet;
        MinesCount = minesCount;
        GridSize = gridSize;
        Mines = PickMines(minePicker ?? DefaultMinePicker(gridSize), minesCount, gridSize);
    }

    public bool Reveal(int cell)
    {
        if (cell < 0 || cell >= GridSize)
            throw new ArgumentOutOfRangeException(nameof(cell), "Célula inválida.");

        if (IsFinished)
            throw new InvalidOperationException("Este campo já encerrou a partida.");

        if (!_revealed.Add(cell))
            throw new InvalidOperationException("Esta célula já foi revelada.");

        if (Mines.Contains(cell))
        {
            HasBoom = true;
            return false;
        }

        CurrentMultiplier = Math.Round(
            CalculateMultiplier(_revealed.Count) * CasinoRules.MinesHouseEdge, 2);
        return true;
    }

    public ulong CashOut()
    {
        if (IsFinished)
            throw new InvalidOperationException("Este campo já encerrou a partida.");

        HasCashedOut = true;
        return (ulong)Math.Floor(Bet * CurrentMultiplier);
    }

    public static double CalculateMultiplier(int revealed, int minesCount, int gridSize)
    {
        double multiplier = 1.0;
        var remaining = gridSize;
        for (var i = 0; i < revealed; i++)
        {
            multiplier *= (double)remaining / (remaining - minesCount);
            remaining--;
        }
        return multiplier;
    }

    private double CalculateMultiplier(int revealed)
        => CalculateMultiplier(revealed, MinesCount, GridSize);

    private static IReadOnlySet<int> PickMines(Func<int> pick, int count, int gridSize)
    {
        var mines = new HashSet<int>();
        while (mines.Count < count)
            mines.Add(pick());
        return mines;
    }

    private static Func<int> DefaultMinePicker(int gridSize)
    {
        var pool = new List<int>(gridSize);
        for (var i = 0; i < gridSize; i++)
            pool.Add(i);
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        var index = 0;
        return () => pool[index++];
    }
}