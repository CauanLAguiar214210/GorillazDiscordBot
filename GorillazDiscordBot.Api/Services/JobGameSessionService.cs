using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Services;

public sealed class JobGameSession
{
    public ulong UserId { get; init; }
    public JobGameKind Kind { get; init; }
    public DateTime StartedAt { get; init; }

    public int RoundIndex { get; set; }
    public int StepIndex { get; set; }
    public bool Revealed { get; set; }
    public int CorrectRounds { get; set; }
    public int Errors { get; set; }
    public int Combo { get; set; }
    public double TotalRaw { get; set; }

    public double LastStepCoins { get; set; }
    public double LastBonus { get; set; }
    public string LastEvent { get; set; } = string.Empty;
    public bool LastWasCorrect { get; set; }

    public bool Finished { get; set; }
    public string FinishedReason { get; set; } = string.Empty;

    public JobGameRound CurrentRound { get; set; } = null!;

    public int TotalRounds { get; init; } = JobGameRules.RoundsPerSession;
    public IReadOnlyList<JobGameRound>? PreparedRounds { get; init; }

    public bool IsMemory => JobGameCatalog.IsMemory(Kind);
    public int StepsForRound => JobGameCatalog.StepsForRound(Kind, RoundIndex);
    public int ErrorsLeft => JobGameRules.MaxErrors - Errors;
    public bool IsTimeout => DateTime.UtcNow - StartedAt >= JobGameRules.SessionTimeout;
}

public enum JobGamePlayResult
{
    StepCorrect,
    RoundCleared,
    Error,
    Finished,
    Invalid
}

public sealed class JobGameSessionService
{
    private readonly ConcurrentDictionary<ulong, JobGameSession> _sessions = new();
    private readonly Random _rng = new();
    private readonly Func<double> _roll;
    private readonly Func<JobGameKind, int, int, Random, JobGameRound> _roundFactory;

    public JobGameSessionService(
        Func<double>? roll = null,
        Func<JobGameKind, int, int, Random, JobGameRound>? roundFactory = null)
    {
        _roll = roll ?? (() => Random.Shared.NextDouble());
        _roundFactory = roundFactory ?? JobGameCatalog.Round;
    }

    public bool TryStart(ulong userId, JobGameKind kind, out JobGameSession session, int? rounds = null)
    {
        bool IsActive(JobGameSession s)
            => !s.Finished && DateTime.UtcNow - s.StartedAt < JobGameRules.SessionTimeout;

        if (_sessions.TryGetValue(userId, out var existing) && IsActive(existing))
        {
            session = existing;
            return false;
        }

        var total = rounds is { } requested
            ? JobGameCatalog.ClampRounds(kind, requested)
            : JobGameCatalog.RoundsFor(kind);

        var created = new JobGameSession
        {
            UserId = userId,
            Kind = kind,
            StartedAt = DateTime.UtcNow,
            Revealed = !JobGameCatalog.IsMemory(kind),
            TotalRounds = total,
            PreparedRounds = JobGameCatalog.PrepareRounds(kind, total, _rng)
        };
        created.CurrentRound = NextRound(created);

        session = _sessions.AddOrUpdate(userId, created, (_, _) => created);
        return ReferenceEquals(session, created);
    }

    public JobGameSession? Get(ulong userId)
        => _sessions.TryGetValue(userId, out var session) ? session : null;

    public void Remove(ulong userId)
        => _sessions.TryRemove(userId, out _);

    public void Finish(ulong userId)
    {
        if (Get(userId) is not { } session || session.Finished) return;

        if (session.IsTimeout)
            session.FinishedReason = "Tempo esgotado";

        session.Finished = true;
    }

    public JobGamePlayResult Ready(ulong userId, out JobGameSession session)
    {
        session = Get(userId)!;
        if (session == null) return JobGamePlayResult.Invalid;
        if (session.Finished) return JobGamePlayResult.Finished;
        if (!session.IsMemory || session.Revealed) return JobGamePlayResult.Invalid;

        session.Revealed = true;
        session.CurrentRound = NextRound(session);
        return JobGamePlayResult.StepCorrect;
    }

    public JobGamePlayResult TryPlay(ulong userId, int optionIndex, out JobGameSession session)
    {
        session = Get(userId)!;
        if (session == null) return JobGamePlayResult.Invalid;
        if (session.Finished) return JobGamePlayResult.Finished;

        if (session.IsTimeout)
        {
            session.Finished = true;
            session.FinishedReason = "Tempo esgotado";
            return JobGamePlayResult.Finished;
        }

        if (!session.Revealed) return JobGamePlayResult.Invalid;

        session.LastBonus = 0;
        session.LastEvent = string.Empty;

        var round = session.CurrentRound;
        if (!round.IsValid || optionIndex != round.CorrectIndex)
        {
            session.LastWasCorrect = false;
            session.Errors++;
            session.Combo = 0;

            if (session.Errors >= JobGameRules.MaxErrors)
            {
                session.Finished = true;
                session.FinishedReason = $"{JobGameRules.MaxErrors} erros";
                return JobGamePlayResult.Finished;
            }

            return JobGamePlayResult.Error;
        }

        session.LastWasCorrect = true;
        session.Combo++;

        var stepPay = JobGameCatalog.BasePerRound(session.Kind) * JobGameRules.ComboMultiplier(session.Combo);
        session.TotalRaw += stepPay;
        session.LastStepCoins = stepPay;

        if (round.Bonus > 0)
        {
            session.LastBonus = round.Bonus;
            session.LastEvent = round.Event ?? "Bônus";
            session.TotalRaw += round.Bonus;
        }

        var roll = _roll();
        if (roll < JobGameRules.GorjetaChance)
        {
            session.LastBonus += JobGameRules.GorjetaReward;
            session.LastEvent = Combine(session.LastEvent, "Gorjeta");
            session.TotalRaw += JobGameRules.GorjetaReward;
        }
        else if (roll < JobGameRules.GorjetaChance + JobGameRules.VipChance)
        {
            var extra = stepPay * (JobGameRules.VipMultiplier - 1);
            session.LastBonus += extra;
            session.LastEvent = Combine(session.LastEvent, "Cliente VIP");
            session.TotalRaw += extra;
        }

        session.StepIndex++;
        var result = JobGamePlayResult.StepCorrect;

        if (session.StepIndex >= session.StepsForRound)
        {
            session.CorrectRounds++;
            session.RoundIndex++;
            session.StepIndex = 0;
            session.Revealed = !session.IsMemory;
            result = JobGamePlayResult.RoundCleared;

            if (session.RoundIndex >= session.TotalRounds)
            {
                session.Finished = true;
                session.FinishedReason = "Todas as rodadas concluídas";
                return JobGamePlayResult.Finished;
            }
        }

        session.CurrentRound = NextRound(session);
        return result;
    }

    private JobGameRound NextRound(JobGameSession session)
        => session.PreparedRounds is { Count: > 0 } prepared
            ? prepared[session.RoundIndex]
            : _roundFactory(session.Kind, session.RoundIndex, session.StepIndex, _rng);

    private static string Combine(string current, string next)
        => string.IsNullOrEmpty(current) ? next : $"{current} + {next}";
}