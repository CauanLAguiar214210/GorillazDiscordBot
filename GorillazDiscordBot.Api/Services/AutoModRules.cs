using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

public sealed record AutoModVerdict(bool ShouldAct, AutomodAction Action, string Reason)
{
    public static AutoModVerdict None { get; } = new(false, AutomodAction.Delete, string.Empty);
}

public static class AutoModRules
{
    public static bool ContainsBlockedWord(string? content, IEnumerable<string> blockedWords, out string matchedWord)
    {
        matchedWord = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lowered = content.ToLowerInvariant();
        foreach (var raw in blockedWords)
        {
            var word = raw.Trim().ToLowerInvariant();
            if (word.Length == 0)
                continue;

            if (lowered.Contains(word, StringComparison.Ordinal))
            {
                matchedWord = raw;
                return true;
            }
        }

        return false;
    }

    public static bool IsFlooding(AutomodSettings settings, IEnumerable<DateTime> recentTimestamps, DateTime utcNow)
    {
        if (recentTimestamps == null)
            return false;

        var window = TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSeconds));
        var minAge = utcNow - window;

        var count = recentTimestamps.Count(t => t >= minAge && t <= utcNow);
        return count > settings.MaxMessagesPerInterval;
    }
}