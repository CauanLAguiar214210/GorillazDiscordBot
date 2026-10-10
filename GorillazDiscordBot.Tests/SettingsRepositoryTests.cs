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
        var stored = new Guild { GuildId = 1, Prefix = new PrefixSettings { Prefix = "!" } };
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
        result.Prefix.Prefix.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ComFalhaNoBanco_RetornaSettingsPadrao()
    {
        var (repo, collection) = CreateRepo();
        collection
            .FindAsync<Guild>(
                Arg.Any<FilterDefinition<Guild>>(),
                Arg.Any<FindOptions<Guild, Guild>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IAsyncCursor<Guild>>(new InvalidOperationException("boom")));

        var result = await repo.GetAsync(7);

        result.GuildId.Should().Be(7);
        result.Prefix.Prefix.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ChamadoDuasVezes_ConsultaBancoApenasUmaVez()
    {
        var (repo, collection) = CreateRepo();
        var stored = new Guild { GuildId = 1, Prefix = new PrefixSettings { Prefix = "!" } };
        StubFindAsync(collection, true, stored);

        await repo.GetAsync(1);
        await repo.GetAsync(1);

        _ = collection.Received(1).FindAsync<Guild>(
            Arg.Any<FilterDefinition<Guild>>(),
            Arg.Any<FindOptions<Guild, Guild>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_PersisteNoBanco()
    {
        var (repo, collection) = CreateRepo();
        var settings = new Guild { GuildId = 2, Prefix = new PrefixSettings { Prefix = "!" } };

        var result = await repo.SaveAsync(settings);

        result.Should().BeTrue();
        await collection.Received(1).ReplaceOneAsync(
            Arg.Any<FilterDefinition<Guild>>(),
            settings,
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveAsync_FalhaNoBanco_RetornaFalseEmVezDeEngolirErro()
    {
        var (repo, collection) = CreateRepo();
        collection
            .ReplaceOneAsync(
                Arg.Any<FilterDefinition<Guild>>(),
                Arg.Any<Guild>(),
                Arg.Any<ReplaceOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MongoDB.Driver.ReplaceOneResult>(
                new InvalidOperationException("boom")));

        var settings = new Guild { GuildId = 2 };

        var result = await repo.SaveAsync(settings);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MutateAsync_MutacaoAplicada_PersisteERetornaTrue()
    {
        var (repo, collection) = CreateRepo();
        StubFindAsync(collection, false);

        var mutated = false;
        var result = await repo.MutateAsync(10, guild =>
        {
            guild.JoinVoiceSound = "local:teste.mp3";
            mutated = true;
            return true;
        });

        result.Should().BeTrue();
        mutated.Should().BeTrue();
        await collection.Received(1).ReplaceOneAsync(
            Arg.Any<FilterDefinition<Guild>>(),
            Arg.Any<Guild>(),
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MutateAsync_MutacaoRecusada_NaoPersisteERetornaTrue()
    {
        var (repo, collection) = CreateRepo();
        StubFindAsync(collection, false);

        var result = await repo.MutateAsync(10, _ => false);

        result.Should().BeTrue();
        await collection.DidNotReceive().ReplaceOneAsync(
            Arg.Any<FilterDefinition<Guild>>(),
            Arg.Any<Guild>(),
            Arg.Any<ReplaceOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MutateAsync_FalhaAoPersistir_RetornaFalse()
    {
        var (repo, collection) = CreateRepo();
        StubFindAsync(collection, false);
        collection
            .ReplaceOneAsync(
                Arg.Any<FilterDefinition<Guild>>(),
                Arg.Any<Guild>(),
                Arg.Any<ReplaceOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MongoDB.Driver.ReplaceOneResult>(
                new InvalidOperationException("boom")));

        var result = await repo.MutateAsync(10, _ => true);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MutateAsync_Concorrentes_NaoInterletemGetESave()
    {
        var (repo, collection) = CreateRepo();
        StubFindAsync(collection, false);

        var inFlight = 0;
        var maxInFlight = 0;
        Func<Guild, bool> mutate = _ =>
        {
            var current = Interlocked.Increment(ref inFlight);
            maxInFlight = Math.Max(maxInFlight, current);
            Thread.Sleep(10);
            Interlocked.Decrement(ref inFlight);
            return true;
        };

        await Task.WhenAll(
            repo.MutateAsync(10, mutate),
            repo.MutateAsync(10, mutate));

        maxInFlight.Should().Be(1);
    }

    [Fact]
    public async Task ResetAsync_RemoveDoBancoELimpaCache()
    {
        var (repo, collection) = CreateRepo();
        var stored = new Guild { GuildId = 3, Prefix = new PrefixSettings { Prefix = "!" } };
        StubFindAsync(collection, true, stored);

        await repo.GetAsync(3);
        await repo.ResetAsync(3);
        await repo.GetAsync(3);

        await collection.Received(1).DeleteOneAsync(
            Arg.Any<FilterDefinition<Guild>>(),
            Arg.Any<CancellationToken>());
        _ = collection.Received(2).FindAsync<Guild>(
            Arg.Any<FilterDefinition<Guild>>(),
            Arg.Any<FindOptions<Guild, Guild>>(),
            Arg.Any<CancellationToken>());
    }

    private static (SettingsRepository<Guild> Repo, IMongoCollection<Guild> Collection)
        CreateRepo()
    {
        var collection = Substitute.For<IMongoCollection<Guild>>();
        var repo = new SettingsRepository<Guild>(
            collection,
            NullLogger<SettingsRepository<Guild>>.Instance);
        return (repo, collection);
    }

    private static void StubFindAsync(
        IMongoCollection<Guild> collection,
        bool hasDocument,
        Guild? stored = null)
    {
        var cursor = Substitute.For<IAsyncCursor<Guild>>();
        cursor.Current.Returns(hasDocument ? new[] { stored! } : Array.Empty<Guild>());
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(true, false);

        collection
            .FindAsync<Guild>(
                Arg.Any<FilterDefinition<Guild>>(),
                Arg.Any<FindOptions<Guild, Guild>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cursor));
    }
}