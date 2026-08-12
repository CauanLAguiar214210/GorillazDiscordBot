using FluentAssertions;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class SettingsRepositoryTests
{
    [Fact]
    public async Task GetAsync_ComRegistroNoBanco_RetornaRegistro()
    {
        var (repo, collection) = CreateRepo();
        var stored = new GuildPrefixSettings { GuildId = 1, Prefix = "!" };
        StubFindAsync(collection, true, stored);

        var result = await repo.GetAsync(1);

        result.Should().BeSameAs(stored);
    }

    [Fact]
    public async Task GetAsync_SemRegistro_RetornaSettingsPadraoComGuildId()
    {
        var (repo, collection) = CreateRepo();
        StubFindAsync(collection, false);

        var result = await repo.GetAsync(42);

        result.GuildId.Should().Be(42);
        result.Prefix.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ComFalhaNoBanco_RetornaSettingsPadrao()
    {
        var (repo, collection) = CreateRepo();
        collection
            .FindAsync<GuildPrefixSettings>(
                Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
                Arg.Any<FindOptions<GuildPrefixSettings, GuildPrefixSettings>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IAsyncCursor<GuildPrefixSettings>>(new InvalidOperationException("boom")));

        var result = await repo.GetAsync(7);

        result.GuildId.Should().Be(7);
        result.Prefix.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ChamadoDuasVezes_ConsultaBancoApenasUmaVez()
    {
        var (repo, collection) = CreateRepo();
        var stored = new GuildPrefixSettings { GuildId = 1, Prefix = "!" };
        StubFindAsync(collection, true, stored);

        await repo.GetAsync(1);
        await repo.GetAsync(1);

        _ = collection.Received(1).FindAsync<GuildPrefixSettings>(
            Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
            Arg.Any<FindOptions<GuildPrefixSettings, GuildPrefixSettings>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_PersisteNoBanco()
    {
        var (repo, collection) = CreateRepo();
        var settings = new GuildPrefixSettings { GuildId = 2, Prefix = "!" };

        await repo.SaveAsync(settings);

        await collection.Received(1).ReplaceOneAsync(
            Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
            settings,
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_RemoveDoBancoELimpaCache()
    {
        var (repo, collection) = CreateRepo();
        var stored = new GuildPrefixSettings { GuildId = 3, Prefix = "!" };
        StubFindAsync(collection, true, stored);

        await repo.GetAsync(3);
        await repo.ResetAsync(3);
        await repo.GetAsync(3);

        await collection.Received(1).DeleteOneAsync(
            Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
            Arg.Any<CancellationToken>());
        _ = collection.Received(2).FindAsync<GuildPrefixSettings>(
            Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
            Arg.Any<FindOptions<GuildPrefixSettings, GuildPrefixSettings>>(),
            Arg.Any<CancellationToken>());
    }

    private static (SettingsRepository<GuildPrefixSettings> Repo, IMongoCollection<GuildPrefixSettings> Collection)
        CreateRepo()
    {
        var collection = Substitute.For<IMongoCollection<GuildPrefixSettings>>();
        var repo = new SettingsRepository<GuildPrefixSettings>(
            collection,
            NullLogger<SettingsRepository<GuildPrefixSettings>>.Instance);
        return (repo, collection);
    }

    private static void StubFindAsync(
        IMongoCollection<GuildPrefixSettings> collection,
        bool hasDocument,
        GuildPrefixSettings? stored = null)
    {
        var cursor = Substitute.For<IAsyncCursor<GuildPrefixSettings>>();
        cursor.Current.Returns(hasDocument ? new[] { stored! } : Array.Empty<GuildPrefixSettings>());
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);

        collection
            .FindAsync<GuildPrefixSettings>(
                Arg.Any<FilterDefinition<GuildPrefixSettings>>(),
                Arg.Any<FindOptions<GuildPrefixSettings, GuildPrefixSettings>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }
}
