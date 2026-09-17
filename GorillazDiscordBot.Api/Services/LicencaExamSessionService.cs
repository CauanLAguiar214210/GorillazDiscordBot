using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Profile;

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
    private readonly ConcurrentDictionary<ulong, LicencaExamSession> _sessions = new();
    private readonly Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> _selector;

    public LicencaExamSessionService()
        : this(DefaultSelector)
    {
    }

    public LicencaExamSessionService(Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
    {
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
        => _sessions.TryGetValue(userId, out var session) ? session : null;

    public bool Cancel(ulong userId)
        => _sessions.TryRemove(userId, out _);

    public bool Remove(ulong userId)
        => _sessions.TryRemove(userId, out _);

    private static IReadOnlyList<MathQuestion> DefaultSelector(IReadOnlyList<MathQuestion> pool, int count)
        => pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
}