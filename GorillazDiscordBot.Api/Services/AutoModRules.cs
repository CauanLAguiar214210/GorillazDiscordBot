using System.Text.RegularExpressions;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Services;

public sealed record AutoModVerdict(bool ShouldAct, AutomodAction Action, string Reason)
{
    public static AutoModVerdict None { get; } = new(false, AutomodAction.Delete, string.Empty);
}

public static class AutoModRules
{
    private static readonly Regex InviteRegex = new(
        @"(?:discord\.gg\/|discord\.com\/invite\/|discord\.app\/invite\/|discordapp\.com\/invite\/)([a-zA-Z0-9]{2,32})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EveryoneRegex = new(
        @"@(everyone|here)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MentionRegex = new(
        @"<@[!&]?\d+>|<@&\d+>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public static bool ContainsInvite(string? content)
        => !string.IsNullOrWhiteSpace(content) && InviteRegex.IsMatch(content);

    public static bool MentionsEveryone(string? content)
        => !string.IsNullOrWhiteSpace(content) && EveryoneRegex.IsMatch(content);

    public static int CountMentions(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return 0;

        return MentionRegex.Matches(content).Count;
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