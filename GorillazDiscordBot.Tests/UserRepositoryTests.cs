using FluentAssertions;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetMainIdAsync_SemPerfil_RetornaProprioId()
    {
        var (repo, _) = CreateRepo(Array.Empty<DiscordUserProfile>());

        var result = await repo.GetMainIdAsync(7);

        result.Should().Be(7);
    }

    [Fact]
    public async Task GetMainIdAsync_ContaPrincipal_RetornaProprioId()
    {
        var (repo, _) = CreateRepo(new[] { new DiscordUserProfile { UserId = 1, MainUserId = 0 } });

        var result = await repo.GetMainIdAsync(1);

        result.Should().Be(1);
    }

    [Fact]
    public async Task GetMainIdAsync_Alt_RetornaMain()
    {
        var (repo, _) = CreateRepo(new[] { new DiscordUserProfile { UserId = 2, MainUserId = 1 } });

        var result = await repo.GetMainIdAsync(2);

        result.Should().Be(1);
    }

    [Fact]
    public async Task GetGroupAsync_Alt_RetornaMainPrimeiroEAltsOrdenados()
    {
        var (repo, _) = CreateRepo(
            new[] { new DiscordUserProfile { UserId = 2, MainUserId = 1 } },
            new[] { new DiscordUserProfile { UserId = 1, MainUserId = 0 } },
            new[]
            {
                new DiscordUserProfile { UserId = 2, MainUserId = 1 },
                new DiscordUserProfile { UserId = 3, MainUserId = 1 }
            });

        var result = await repo.GetGroupAsync(2);

        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(1);
        result.Single(m => m.UserId == 3).Should().NotBeNull();
    }

    [Fact]
    public async Task LinkAsync_AltComPerfil_DefineMain()
    {
        var (repo, collection) = CreateRepo(new[] { new DiscordUserProfile { UserId = 2, MainUserId = 0 } });
        StubUpdateResult(collection, 1L);

        var result = await repo.LinkAsync(mainId: 1, altId: 2);

        result.Should().BeTrue();
        await collection.Received(1).UpdateOneAsync(
            Arg.Any<FilterDefinition<DiscordUserProfile>>(),
            Arg.Any<UpdateDefinition<DiscordUserProfile>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LinkAsync_AltSemPerfil_CriaDocumentoVinculado()
    {
        var (repo, collection) = CreateRepo(Array.Empty<DiscordUserProfile>());

        var result = await repo.LinkAsync(mainId: 1, altId: 2);

        result.Should().BeTrue();
        await collection.Received(1).InsertOneAsync(
            Arg.Is<DiscordUserProfile>(p => p.UserId == 2 && p.MainUserId == 1),
            Arg.Any<InsertOneOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlinkAsync_Alt_ResetaMain()
    {
        var (repo, collection) = CreateRepo(new[] { new DiscordUserProfile { UserId = 2, MainUserId = 1 } });
        StubUpdateResult(collection, 1L);

        var result = await repo.UnlinkAsync(2);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UnlinkAsync_SemVinculo_NaoChamaBanco()
    {
        var (repo, collection) = CreateRepo(new[] { new DiscordUserProfile { UserId = 1, MainUserId = 0 } });

        var result = await repo.UnlinkAsync(1);

        result.Should().BeFalse();
        await collection.DidNotReceive().UpdateOneAsync(
            Arg.Any<FilterDefinition<DiscordUserProfile>>(),
            Arg.Any<UpdateDefinition<DiscordUserProfile>>(),
            Arg.Any<UpdateOptions>(),
            Arg.Any<CancellationToken>());
    }

    private static (UserRepository Repo, IMongoCollection<DiscordUserProfile> Collection) CreateRepo(
        params DiscordUserProfile[][] findSequences)
    {
        var collection = Substitute.For<IMongoCollection<DiscordUserProfile>>();

        var sequence = findSequences.Length == 0
            ? new[] { Task.FromResult(MakeCursor(Array.Empty<DiscordUserProfile>())) }
            : findSequences.Select(s => Task.FromResult(MakeCursor(s))).ToArray();

        collection
            .FindAsync<DiscordUserProfile>(
                Arg.Any<FilterDefinition<DiscordUserProfile>>(),
                Arg.Any<FindOptions<DiscordUserProfile, DiscordUserProfile>>(),
                Arg.Any<CancellationToken>())
            .Returns(sequence.First(), sequence.Skip(1).ToArray());

        var repo = new UserRepository(collection, NullLogger<UserRepository>.Instance);
        return (repo, collection);
    }

    private static IAsyncCursor<DiscordUserProfile> MakeCursor(DiscordUserProfile[] items)
    {
        var cursor = Substitute.For<IAsyncCursor<DiscordUserProfile>>();
        cursor.Current.Returns(items);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(items.Length > 0, false);
        return cursor;
    }

    private static void StubUpdateResult(IMongoCollection<DiscordUserProfile> collection, long modified)
    {
        var updateResult = Substitute.For<UpdateResult>();
        updateResult.ModifiedCount.Returns(modified);
        collection
            .UpdateOneAsync(
                Arg.Any<FilterDefinition<DiscordUserProfile>>(),
                Arg.Any<UpdateDefinition<DiscordUserProfile>>(),
                Arg.Any<UpdateOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(updateResult);
    }
}