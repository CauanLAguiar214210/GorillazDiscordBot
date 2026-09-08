using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Services;

public sealed record AppliedRelic(string Name, string Emoji, RelicEffect Effect, int Value);

public sealed record PayoutResult(
    ulong Balance,
    ulong BaseReturn,
    ulong Bonus,
    AppliedRelic? Relic);