using Discord;

namespace GorillazDiscordBot.Utils;

public static class MessagePurge
{
    public const int MaxBulkPerBatch = 100;
    public const int MaxPurge = 2000;

    public static readonly TimeSpan BulkMaxAge = TimeSpan.FromDays(14);

    public sealed record PurgePlan(List<List<IMessage>> BulkChunks, List<IMessage> Individual);

    public static PurgePlan PlanDelete(IEnumerable<IMessage> messages, ulong? userId, DateTime utcNow)
    {
        IReadOnlyList<IMessage> eligible = messages
            .Where(m => !m.IsPinned)
            .Where(m => userId == null || m.Author.Id == userId.Value)
            .ToList();

        var cutoff = utcNow - BulkMaxAge;

        var bulk = eligible
            .Where(m => m.Timestamp >= cutoff)
            .ToList();

        var individual = eligible
            .Where(m => m.Timestamp < cutoff)
            .ToList();

        var chunks = new List<List<IMessage>>();
        foreach (var chunk in bulk.Chunk(MaxBulkPerBatch))
            chunks.Add(chunk.ToList());

        return new PurgePlan(chunks, individual);
    }
}