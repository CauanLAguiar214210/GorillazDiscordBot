using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Services;

public sealed class JobExamSession
{
    public ulong UserId { get; init; }
    public string JobKey { get; init; } = string.Empty;
    public IReadOnlyList<MathQuestion> Questions { get; init; } = Array.Empty<MathQuestion>();
    public int CurrentIndex { get; set; }
    public int CorrectCount { get; set; }

    public bool IsFinished => CurrentIndex >= Questions.Count;
    public MathQuestion Current => Questions[CurrentIndex];
    public bool Passed => CorrectCount >= JobLicensing.PassingScore;
}

public sealed class JobExamSessionService
{
    private readonly ConcurrentDictionary<ulong, JobExamSession> _sessions = new();
    private readonly Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> _selector;

    public JobExamSessionService()
        : this(DefaultSelector)
    {
    }

    public JobExamSessionService(Func<IReadOnlyList<MathQuestion>, int, IReadOnlyList<MathQuestion>> selector)
    {
        _selector = selector;
    }

    public bool TryStart(ulong userId, string jobKey, out JobExamSession session)
    {
        var created = new JobExamSession
        {
            UserId = userId,
            JobKey = jobKey,
            Questions = _selector(JobLicensing.For(jobKey), JobLicensing.QuestionsPerExam)
        };

        session = _sessions.GetOrAdd(userId, created);
        return ReferenceEquals(session, created);
    }

    public JobExamSession? Get(ulong userId)
        => _sessions.TryGetValue(userId, out var session) ? session : null;

    public bool Cancel(ulong userId)
        => _sessions.TryRemove(userId, out _);

    public bool Remove(ulong userId)
        => _sessions.TryRemove(userId, out _);

    private static IReadOnlyList<MathQuestion> DefaultSelector(IReadOnlyList<MathQuestion> pool, int count)
        => pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
}