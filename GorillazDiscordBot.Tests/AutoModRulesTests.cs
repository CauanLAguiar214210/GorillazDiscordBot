using FluentAssertions;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;

namespace GorillazDiscordBot.Tests;

public class AutoModRulesTests
{
    private static AutomodSettings DefaultSettings() => new()
    {
        Enabled = true,
        MaxMessagesPerInterval = 5,
        IntervalSeconds = 10,
        TimeoutMinutes = 10
    };

    [Theory]
    [InlineData("isso é um teste", "teste", true)]
    [InlineData("isso é um TESTE", "teste", true)]
    [InlineData("xingou limpo", "palavrao", false)]
    [InlineData("", "teste", false)]
    [InlineData("texto", "", false)]
    public void ContainsBlockedWord_ShouldMatch_AccordingToContent(string content, string word, bool expected)
    {
        var match = AutoModRules.ContainsBlockedWord(content, new[] { word }, out var matched);

        match.Should().Be(expected);
        if (expected)
            matched.Should().Be(word);
    }

    [Fact]
    public void ContainsBlockedWord_ShouldReturnFirstMatch()
    {
        var settings = DefaultSettings();
        settings.BlockedWords.AddRange(new[] { "bom", "ruim" });

        var match = AutoModRules.ContainsBlockedWord("eu fui bom e depois ruim", settings.BlockedWords, out var matched);

        match.Should().BeTrue();
        matched.Should().Be("bom");
    }

    [Fact]
    public void ContainsBlockedWord_ShouldIgnoreEmptyEntriesAndNullContent()
    {
        AutoModRules.ContainsBlockedWord(null, new[] { "  ", "abcd" }, out var matchText)
            .Should().BeFalse();
        matchText.Should().BeEmpty();

        AutoModRules.ContainsBlockedWord("abcd", new[] { "   ", string.Empty }, out var matchEmpty)
            .Should().BeFalse();
        matchEmpty.Should().BeEmpty();
    }

    [Fact]
    public void IsFlooding_ShouldBeFalse_BelowOrAtThreshold()
    {
        var settings = DefaultSettings();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var stamps = Enumerable.Range(1, settings.MaxMessagesPerInterval)
            .Select(i => now.AddSeconds(-i))
            .ToList();

        AutoModRules.IsFlooding(settings, stamps, now).Should().BeFalse();
    }

    [Fact]
    public void IsFlooding_ShouldBeTrue_AboveThreshold()
    {
        var settings = DefaultSettings();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var stamps = Enumerable.Range(0, settings.MaxMessagesPerInterval + 1)
            .Select(i => now.AddSeconds(-i))
            .ToList();

        AutoModRules.IsFlooding(settings, stamps, now).Should().BeTrue();
    }

    [Fact]
    public void IsFlooding_ShouldIgnore_TimestampsOutsideWindow()
    {
        var settings = DefaultSettings();
        settings.MaxMessagesPerInterval = 3;
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var stamps = new List<DateTime>
        {
            now.AddSeconds(-1),
            now.AddSeconds(-11),
            now.AddSeconds(-12),
            now.AddSeconds(-13),
            now.AddSeconds(-14)
        };

        AutoModRules.IsFlooding(settings, stamps, now).Should().BeFalse();
    }

    [Fact]
    public void IsFlooding_ShouldBeFalse_ForNullCollection()
        => AutoModRules.IsFlooding(DefaultSettings(), null!, DateTime.UtcNow).Should().BeFalse();
}