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

    [Theory]
    [InlineData("discord.gg/abcd123", true)]
    [InlineData("entra no discord.gg/abcd123 agora", true)]
    [InlineData("https://discord.com/invite/XYZ-9abc", true)]
    [InlineData("https://discordapp.com/invite/ABC-123-def", true)]
    [InlineData("só um texto normal", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ContainsInvite_ShouldMatch_DiscordInvitePatterns(string? content, bool expected)
    {
        AutoModRules.ContainsInvite(content).Should().Be(expected);
    }

    [Theory]
    [InlineData("oi @everyone tudo bem", true)]
    [InlineData("BOM DIA @here", true)]
    [InlineData("texto sem menção", false)]
    [InlineData("", false)]
    public void MentionsEveryone_ShouldMatch_EveryoneAndHere(string? content, bool expected)
    {
        AutoModRules.MentionsEveryone(content).Should().Be(expected);
    }

    [Theory]
    [InlineData("<@123456789>", 1)]
    [InlineData("<@123> <@456>", 2)]
    [InlineData("<@&987> <@&123> <@!555>", 3)]
    [InlineData("nenhuma menção", 0)]
    [InlineData("<@123> por favor <@123> pare", 2)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void CountMentions_ShouldCount_UserAndRoleMentions(string? content, int expected)
    {
        AutoModRules.CountMentions(content).Should().Be(expected);
    }

    [Fact]
    public void IsFlooding_ShouldUse_ConfiguredIntervalWindow()
    {
        var settings = DefaultSettings();
        settings.MaxMessagesPerInterval = 3;
        settings.IntervalSeconds = 30;
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var withinWindow = new List<DateTime>
        {
            now,
            now.AddSeconds(-1),
            now.AddSeconds(-15),
            now.AddSeconds(-29)
        };

        AutoModRules.IsFlooding(settings, withinWindow, now).Should().BeTrue();

        var outsideWindow = new List<DateTime>
        {
            now.AddSeconds(-1),
            now.AddSeconds(-31),
            now.AddSeconds(-32),
            now.AddSeconds(-33)
        };

        AutoModRules.IsFlooding(settings, outsideWindow, now).Should().BeFalse();
    }

    [Fact]
    public void StrikePolicy_ShouldBeOff_WhenStrikesDisabled()
    {
        var settings = DefaultSettings();
        settings.EnableStrikes = false;
        settings.StrikeTimeoutWarnings = 3;
        settings.StrikeBanWarnings = 6;

        StrikePolicy.ShouldTimeout(settings, 3).Should().BeFalse();
        StrikePolicy.ShouldBan(settings, 6).Should().BeFalse();
    }

    [Fact]
    public void StrikePolicy_ShouldTimeout_ExactlyAtThreshold()
    {
        var settings = DefaultSettings();
        settings.EnableStrikes = true;
        settings.StrikeTimeoutWarnings = 3;
        settings.StrikeBanWarnings = 6;

        StrikePolicy.ShouldTimeout(settings, 3).Should().BeTrue();
        StrikePolicy.ShouldTimeout(settings, 4).Should().BeFalse();
    }

    [Fact]
    public void StrikePolicy_ShouldBan_AtOrAboveThreshold()
    {
        var settings = DefaultSettings();
        settings.EnableStrikes = true;
        settings.StrikeTimeoutWarnings = 3;
        settings.StrikeBanWarnings = 6;

        StrikePolicy.ShouldBan(settings, 6).Should().BeTrue();
        StrikePolicy.ShouldBan(settings, 9).Should().BeTrue();
        StrikePolicy.ShouldBan(settings, 5).Should().BeFalse();
    }

    [Fact]
    public void StrikePolicy_ShouldNotTimeout_WhenTimeoutAtOrAboveBanThreshold()
    {
        var settings = DefaultSettings();
        settings.EnableStrikes = true;
        settings.StrikeTimeoutWarnings = 6;
        settings.StrikeBanWarnings = 6;

        StrikePolicy.ShouldTimeout(settings, 6).Should().BeFalse();
        StrikePolicy.ShouldBan(settings, 6).Should().BeTrue();
    }
}