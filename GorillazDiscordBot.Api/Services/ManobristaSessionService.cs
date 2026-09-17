using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Services;

public sealed class ManobristaSession
{
    public ulong UserId { get; init; }
    public int CarrosEstacionados { get; set; }
    public DateTime StartedAt { get; init; }
    public bool Finished { get; set; }

    public int Combo { get; set; }
    public DateTime? LastClickAt { get; set; }
    public double TotalRaw { get; set; }
    public double LastClickCoins { get; set; }
    public ManobristaEvent LastEvent { get; set; }

    public int Vagas { get; init; }
    public double BaseValue { get; init; }

    public bool IsFull => CarrosEstacionados >= Vagas;
}

public sealed class ManobristaSessionService
{
    private readonly ConcurrentDictionary<ulong, ManobristaSession> _sessions = new();
    private readonly Func<double> _roll;

    public ManobristaSessionService(Func<double>? roll = null)
        => _roll = roll ?? (() => Random.Shared.NextDouble());

    public bool TryStart(ulong userId, out ManobristaSession session)
        => TryStart(userId, ManobristaRules.Vagas, ManobristaRules.BasePerCar, out session);

    public bool TryStart(ulong userId, int vagas, double baseValue, out ManobristaSession session)
    {
        bool IsActive(ManobristaSession s)
            => !s.Finished && DateTime.UtcNow - s.StartedAt < ManobristaRules.SessionTimeout;

        if (_sessions.TryGetValue(userId, out var existing) && IsActive(existing))
        {
            session = existing;
            return false;
        }

        var created = new ManobristaSession
        {
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            Vagas = Math.Max(1, vagas),
            BaseValue = Math.Max((double)ManobristaRules.BasePerCar, baseValue)
        };

        session = _sessions.AddOrUpdate(userId, created, (_, _) => created);
        return ReferenceEquals(session, created);
    }

    public ManobristaSession? Get(ulong userId)
        => _sessions.TryGetValue(userId, out var session) ? session : null;

    public void Remove(ulong userId)
        => _sessions.TryRemove(userId, out _);

    public bool TryPark(ulong userId)
    {
        var session = Get(userId);
        if (session == null || session.Finished || session.IsFull) return false;

        var now = DateTime.UtcNow;
        session.Combo = session.LastClickAt is { } last && now - last <= ManobristaRules.ComboWindow
            ? session.Combo + 1
            : 1;
        session.LastClickAt = now;

        var clickValue = session.BaseValue * ManobristaRules.ComboMultiplier(session.Combo);
        session.TotalRaw += clickValue;
        session.LastClickCoins = clickValue;
        session.LastEvent = ResolveEvent();

        if (session.LastEvent == ManobristaEvent.Gorjeta)
            session.TotalRaw += ManobristaRules.GorjetaReward;
        else if (session.LastEvent == ManobristaEvent.Vip)
            session.TotalRaw += clickValue * (ManobristaRules.VipMultiplier - 1);
        else if (session.LastEvent == ManobristaEvent.Riscado)
            session.CarrosEstacionados--;

        session.CarrosEstacionados++;
        if (session.CarrosEstacionados < 0) session.CarrosEstacionados = 0;

        if (session.IsFull) session.Finished = true;
        return true;
    }

    public void Finish(ulong userId)
    {
        if (Get(userId) is { } session) session.Finished = true;
    }

    private ManobristaEvent ResolveEvent()
    {
        var roll = _roll();
        if (roll < ManobristaRules.GorjetaChance) return ManobristaEvent.Gorjeta;
        if (roll < ManobristaRules.GorjetaChance + ManobristaRules.VipChance) return ManobristaEvent.Vip;
        if (roll < ManobristaRules.GorjetaChance + ManobristaRules.VipChance + ManobristaRules.RiscadoChance)
            return ManobristaEvent.Riscado;
        return ManobristaEvent.Nenhum;
    }
}
