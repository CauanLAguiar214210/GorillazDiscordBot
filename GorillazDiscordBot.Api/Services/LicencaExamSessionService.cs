using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;

namespace GorillazDiscordBot.Services;

public sealed class LicencaExamSession
{
    public ulong UserId { get; init; }
    public LicenseLevel Level { get; init; }
    public IReadOnlyList<MathQuestion> Questions { get; init; } = Array.Empty<MathQuestion>();
    public int CurrentIndex { get; set; }
    public int CorrectCount { get; set; }

    public bool IsFinished => CurrentIndex >= Questions.Count;
    public MathQuestion Current => Questions[CurrentIndex];
    public bool Passed => CorrectCount >= LicenseQuizzes.PassingScore;
}

public sealed class LicencaExamSessionService
{
    private readonly ISessionStore<ulong, LicencaExamSession> _sessions;
    private readonly Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> _selector;

    public LicencaExamSessionService()
        : this(DefaultSelector)
    {
    }

    public LicencaExamSessionService(Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
        : this(new InMemorySessionStore<ulong, LicencaExamSession>(), selector)
    {
    }

    internal LicencaExamSessionService(
        ISessionStore<ulong, LicencaExamSession> sessions,
        Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
    {
        _sessions = sessions;
        _selector = selector;
    }

    public bool TryStart(ulong userId, LicenseLevel level, out LicencaExamSession session)
    {
        var created = new LicencaExamSession
        {
            UserId = userId,
            Level = level,
            Questions = _selector(LicenseQuizzes.For(level), LicenseQuizzes.QuestionsPerExam)
        };

        session = _sessions.GetOrAdd(userId, created);
        return ReferenceEquals(session, created);
    }

    public LicencaExamSession? Get(ulong userId)
        => _sessions.TryGet(userId, out var session) ? session : null;

    public bool Cancel(ulong userId)
        => _sessions.TryRemove(userId);

    public bool Remove(ulong userId)
        => _sessions.TryRemove(userId);

    private static IReadOnlyList<MathQuestion> DefaultSelector(IReadOnlyList<MathQuestion> pool, int count)
        => pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
}