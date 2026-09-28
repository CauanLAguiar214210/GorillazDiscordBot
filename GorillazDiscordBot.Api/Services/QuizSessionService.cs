using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public sealed class QuizSession
{
    public ulong UserId { get; init; }
    public SchoolingLevel Level { get; init; }
    public IReadOnlyList<MathQuestion> Questions { get; init; } = Array.Empty<MathQuestion>();
    public int CurrentIndex { get; set; }
    public int CorrectCount { get; set; }

    public bool IsFinished => CurrentIndex >= Questions.Count;
    public MathQuestion Current => Questions[CurrentIndex];
    public bool Passed => CorrectCount >= QuizSessionService.PassingScore;
}

public sealed class QuizSessionService
{
    public const int PassingScore = 2;

    private readonly ISessionStore<ulong, QuizSession> _sessions;
    private readonly Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> _selector;

    public QuizSessionService()
        : this(DefaultSelector)
    {
    }

    public QuizSessionService(Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
        : this(new InMemorySessionStore<ulong, QuizSession>(), selector)
    {
    }

    internal QuizSessionService(
        ISessionStore<ulong, QuizSession> sessions,
        Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
    {
        _sessions = sessions;
        _selector = selector;
    }

    public bool TryStart(ulong userId, SchoolingLevel level, out QuizSession session)
    {
        var created = new QuizSession
        {
            UserId = userId,
            Level = level,
            Questions = _selector(SchoolingQuizzes.For(level), SchoolingQuizzes.QuestionsPerExam)
        };

        session = _sessions.GetOrAdd(userId, created);
        return ReferenceEquals(session, created);
    }

    public QuizSession? Get(ulong userId)
        => _sessions.TryGet(userId, out var session) ? session : null;

    public bool Cancel(ulong userId)
        => _sessions.TryRemove(userId);

    public bool Remove(ulong userId)
        => _sessions.TryRemove(userId);

    private static IReadOnlyList<MathQuestion> DefaultSelector(IReadOnlyList<MathQuestion> pool, int count)
        => pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
}