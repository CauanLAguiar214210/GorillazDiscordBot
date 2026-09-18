using Discord;
using FluentAssertions;
using GorillazDiscordBot.Utils;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class MessagePurgeTests
{
    private static readonly DateTime UtcNow = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PlanDelete_Should_SplitOldMessagesIntoIndividual()
    {
        var recent = CreateMessage(1, UtcNow.AddMinutes(-1));
        var old = CreateMessage(2, UtcNow.AddDays(-15));

        var plan = MessagePurge.PlanDelete(new[] { recent, old }, null, UtcNow);

        plan.BulkChunks.Should().HaveCount(1);
        plan.BulkChunks[0].Should().Contain(recent);
        plan.Individual.Should().ContainSingle().Which.Should().Be(old);
    }

    [Fact]
    public void PlanDelete_Should_PreservePinnedMessages()
    {
        var normal = CreateMessage(1, UtcNow);
        var pinned = CreateMessage(2, UtcNow, isPinned: true);

        var plan = MessagePurge.PlanDelete(new[] { normal, pinned }, null, UtcNow);

        plan.BulkChunks.SelectMany(c => c).Should().ContainSingle().Which.Should().Be(normal);
        plan.Individual.Should().BeEmpty();
    }

    [Fact]
    public void PlanDelete_Should_FilterByUserId()
    {
        var fromUserA = CreateMessage(1, UtcNow);
        var fromUserAOld = CreateMessage(1, UtcNow.AddDays(-20));
        var fromUserB = CreateMessage(2, UtcNow);

        var plan = MessagePurge.PlanDelete(new[] { fromUserA, fromUserAOld, fromUserB }, 1, UtcNow);

        plan.BulkChunks.SelectMany(c => c).Should().ContainSingle().Which.Should().Be(fromUserA);
        plan.Individual.Should().ContainSingle().Which.Should().Be(fromUserAOld);
    }

    [Fact]
    public void PlanDelete_Should_ChunkBulkIntoBatchesOfHundred()
    {
        var messages = Enumerable.Range(1, 250)
            .Select(i => CreateMessage((ulong)i, UtcNow))
            .ToArray();

        var plan = MessagePurge.PlanDelete(messages, null, UtcNow);

        plan.BulkChunks.Should().HaveCount(3);
        plan.BulkChunks[0].Should().HaveCount(100);
        plan.BulkChunks[1].Should().HaveCount(100);
        plan.BulkChunks[2].Should().HaveCount(50);
        plan.Individual.Should().BeEmpty();
    }

    private static IMessage CreateMessage(ulong authorId, DateTimeOffset timestamp, bool isPinned = false)
    {
        var author = Substitute.For<IUser>();
        author.Id.Returns(authorId);

        var message = Substitute.For<IMessage>();
        message.Author.Returns(author);
        message.Timestamp.Returns(timestamp);
        message.IsPinned.Returns(isPinned);
        return message;
    }
}