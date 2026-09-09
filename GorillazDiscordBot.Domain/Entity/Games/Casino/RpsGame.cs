namespace GorillazDiscordBot.Domain.Entity.Games.Casino;

public enum RpsMove
{
    Pedra,
    Papel,
    Tesoura
}

public enum RpsOutcome
{
    PlayerWin,
    Draw,
    PlayerLose
}

public sealed class RpsGame
{
    private readonly Func<RpsMove> _opponentMove;

    public RpsMove PlayerMove { get; }

    public RpsMove? OpponentMove { get; private set; }

    public RpsOutcome? Outcome { get; private set; }

    public bool HasPlayed => Outcome.HasValue;

    public bool IsWin => Outcome == RpsOutcome.PlayerWin;

    public bool IsDraw => Outcome == RpsOutcome.Draw;

    public RpsGame(RpsMove playerMove, Func<RpsMove>? opponentMove = null)
    {
        PlayerMove = playerMove;
        _opponentMove = opponentMove ?? (() => (RpsMove)Random.Shared.Next(3));
    }

    public RpsMove Play()
    {
        if (HasPlayed)
            throw new InvalidOperationException("Esta rodada já foi disputada.");

        var opponent = _opponentMove();
        OpponentMove = opponent;
        Outcome = Resolve(PlayerMove, opponent);
        return opponent;
    }

    public ulong CalculateReturn(ulong bet)
    {
        if (bet <= 0)
            throw new ArgumentOutOfRangeException(nameof(bet), "A aposta deve ser positiva.");

        if (!HasPlayed)
            return 0;

        return Outcome switch
        {
            RpsOutcome.PlayerWin => bet * (ulong)CasinoRules.RpsPayout,
            RpsOutcome.Draw => bet,
            _ => 0
        };
    }

    public static RpsOutcome Resolve(RpsMove player, RpsMove opponent)
    {
        if (player == opponent)
            return RpsOutcome.Draw;

        var beats = (player, opponent) switch
        {
            (RpsMove.Pedra, RpsMove.Tesoura) => true,
            (RpsMove.Tesoura, RpsMove.Papel) => true,
            (RpsMove.Papel, RpsMove.Pedra) => true,
            _ => false
        };

        return beats ? RpsOutcome.PlayerWin : RpsOutcome.PlayerLose;
    }
}