namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public sealed class RaceGame
{
    private readonly Func<int> _winner;

    public int Runners { get; }

    public int PlayerPick { get; }

    public int? WinnerIndex { get; private set; }

    public bool HasFinished => WinnerIndex.HasValue;

    public bool IsWin => WinnerIndex == PlayerPick;

    public RaceGame(int playerPick, int runners = CasinoRules.RaceRunners, Func<int>? winnerPicker = null)
    {
        if (playerPick < 0 || playerPick >= runners)
            throw new ArgumentOutOfRangeException(nameof(playerPick), "Corredor inválido.");

        PlayerPick = playerPick;
        Runners = runners;
        _winner = winnerPicker ?? (() => Random.Shared.Next(runners));
    }

    public int Start()
    {
        if (HasFinished)
            throw new InvalidOperationException("Esta corrida já terminou.");

        WinnerIndex = _winner();
        return WinnerIndex.Value;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        return IsWin ? bet * (ulong)(Runners - 1) : 0;
    }
}