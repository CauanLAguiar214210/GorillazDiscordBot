using FluentAssertions;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Entity.Release;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ReleaseNoteRepositoryTests
{
    [Fact]
    public async Task GetUnannouncedAsync_RetornaItensDoCursor()
    {
        var (repo, collection) = CreateRepo();
        StubFind(collection, CreateRelease("v1.2.0"));

        var result = await repo.GetUnannouncedAsync(DateTime.UtcNow, 20);

        result.Should().ContainSingle();
        result[0].Version.Should().Be("v1.2.0");
    }

    [Fact]
    public async Task GetRecentAsync_RetornaItensDoCursor()
    {
        var (repo, collection) = CreateRepo();
        StubFind(collection, CreateRelease("v2.0.0"), CreateRelease("v1.1.0"));

        var result = await repo.GetRecentAsync(10);

        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Version == "v2.0.0");
    }

    [Fact]
    public async Task GetByVersionAsync_SemDocumento_RetornaNull()
    {
        var (repo, collection) = CreateRepo();
        StubFind(collection);

        var result = await repo.GetByVersionAsync("v9.9.9");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByVersionAsync_ComDocumento_RetornaRegistro()
    {
        var (repo, collection) = CreateRepo();
        StubFind(collection, CreateRelease("v1.1.0"));

        var result = await repo.GetByVersionAsync("v1.1.0");

        result.Should().NotBeNull();
        result!.Version.Should().Be("v1.1.0");
    }

    [Fact]
    public async Task GetLatestAsync_RetornaPrimeiroDocumento()
    {
        var (repo, collection) = CreateRepo();
        StubFind(collection, CreateRelease("v1.1.0"));

        var result = await repo.GetLatestAsync();

        result.Should().NotBeNull();
        result!.Version.Should().Be("v1.1.0");
    }

    [Fact]
    public async Task TryMarkAnnouncedAsync_QuandoAtualiza_RetornaTrue()
    {
        var (repo, collection) = CreateRepo();
        StubUpdateResult(collection, 1L);

        var result = await repo.TryMarkAnnouncedAsync("v1.1.0", DateTime.UtcNow);

        result.Should().BeTrue();
        await collection.Received(1).UpdateOneAsync(
            Arg.Any<FilterDefinition<ReleaseNote>>(),
            Arg.Any<UpdateDefinition<ReleaseNote>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryMarkAnnouncedAsync_QuandoNadaAtualiza_RetornaFalse()
    {
        var (repo, collection) = CreateRepo();
        StubUpdateResult(collection, 0L);

        var result = await repo.TryMarkAnnouncedAsync("v1.1.0", DateTime.UtcNow);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureIndexesAsync_CriaIndices()
    {
        var (repo, collection) = CreateRepo();

        await repo.EnsureIndexesAsync();

        await collection.Indexes.Received(1).CreateManyAsync(
            Arg.Any<IEnumerable<CreateIndexModel<ReleaseNote>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureIndexesAsync_ComFalhaNoBanco_NaoLanca()
    {
        var (repo, collection) = CreateRepo();
        collection.Indexes
            .CreateManyAsync(
                Arg.Any<IEnumerable<CreateIndexModel<ReleaseNote>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IEnumerable<string>>(new InvalidOperationException("boom")));

        var act = () => repo.EnsureIndexesAsync();

        await act.Should().NotThrowAsync();
    }

    private static (ReleaseNoteRepository Repo, IMongoCollection<ReleaseNote> Collection) CreateRepo()
    {
        var collection = Substitute.For<IMongoCollection<ReleaseNote>>();
        var repo = new ReleaseNoteRepository(collection, NullLogger<ReleaseNoteRepository>.Instance);
        return (repo, collection);
    }

    private static void StubFind(IMongoCollection<ReleaseNote> collection, params ReleaseNote[] items)
    {
        var cursor = Substitute.For<IAsyncCursor<ReleaseNote>>();
        cursor.Current.Returns(items);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);

        collection
            .FindAsync<ReleaseNote>(
                Arg.Any<FilterDefinition<ReleaseNote>>(),
                Arg.Any<FindOptions<ReleaseNote, ReleaseNote>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }

    private static void StubUpdateResult(IMongoCollection<ReleaseNote> collection, long modified)
    {
        var updateResult = Substitute.For<UpdateResult>();
        updateResult.ModifiedCount.Returns(modified);
        collection
            .UpdateOneAsync(
                Arg.Any<FilterDefinition<ReleaseNote>>(),
                Arg.Any<UpdateDefinition<ReleaseNote>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(updateResult);
    }

    private static ReleaseNote CreateRelease(string version)
        => new()
        {
            Version = version,
            Title = "Atualização",
            PublishedAt = new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            Features = new List<ReleaseFeature>
            {
                new() { Type = ReleaseFeatureType.New, Title = "Novidade" }
            }
        };
}
