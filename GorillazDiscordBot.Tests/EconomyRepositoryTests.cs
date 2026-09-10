using FluentAssertions;
using GorillazDiscordBot.Data.Repository;
using GorillazDiscordBot.Domain.Entity.Economy;
using MongoDB.Driver;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class EconomyRepositoryTests
{
    private readonly IMongoCollection<EconomyProfile> _profiles = Substitute.For<IMongoCollection<EconomyProfile>>();
    private readonly IMongoCollection<EconomyTransaction> _transactions = Substitute.For<IMongoCollection<EconomyTransaction>>();

    [Fact]
    public async Task GetOrCreateAsync_PrimeiraVez_ConcedeBonusBemVindo()
    {
        EconomyProfile? inserted = null;
        EconomyTransaction? logged = null;
        _profiles.InsertOneAsync(
            Arg.Do<EconomyProfile>(p => inserted = p), Arg.Any<InsertOneOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _transactions.InsertOneAsync(
            Arg.Do<EconomyTransaction>(t => logged = t), Arg.Any<InsertOneOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var repo = CreateRepository(Cursor());

        var result = await repo.GetOrCreateAsync(42, "novo");

        result.Money.Should().Be(EconomyRules.WelcomeBonus);
        inserted.Should().NotBeNull();
        inserted!.UserId.Should().Be(42);
        inserted!.Username.Should().Be("novo");
        inserted!.Money.Should().Be(EconomyRules.WelcomeBonus);
        logged.Should().NotBeNull();
        logged!.UserId.Should().Be(42);
        logged!.Type.Should().Be(EconomyTransactionType.Welcome);
        logged!.Amount.Should().Be(EconomyRules.WelcomeBonus);
        logged!.Description.Should().Be("Bônus de boas-vindas");
        _ = _profiles.DidNotReceiveWithAnyArgs().UpdateOneAsync(null!, null!);
    }

    [Fact]
    public async Task GetOrCreateAsync_UsuarioExistente_NaoConcedeBonus()
    {
        var existing = new EconomyProfile { UserId = 42, Username = "velho", Money = 150 };
        var repo = CreateRepository(Cursor(existing));

        var result = await repo.GetOrCreateAsync(42, "velho");

        result.Money.Should().Be(150);
        _ = _profiles.DidNotReceiveWithAnyArgs().InsertOneAsync(null!, null!, default);
        _ = _transactions.DidNotReceiveWithAnyArgs().InsertOneAsync(null!, null!, default);
    }

    [Fact]
    public async Task GetOrCreateAsync_UsernameDiferente_AtualizaSemConcederBonus()
    {
        var existing = new EconomyProfile { UserId = 42, Username = "velho", Money = 150 };
        var repo = CreateRepository(Cursor(existing));

        var result = await repo.GetOrCreateAsync(42, "novoNome");

        result.Money.Should().Be(150);
        _ = _profiles.Received(1).UpdateOneAsync(Arg.Any<FilterDefinition<EconomyProfile>>(), Arg.Any<UpdateDefinition<EconomyProfile>>());
        _ = _profiles.DidNotReceiveWithAnyArgs().InsertOneAsync(null!, null!, default);
        _ = _transactions.DidNotReceiveWithAnyArgs().InsertOneAsync(null!, null!, default);
    }

    private EconomyRepository CreateRepository(IAsyncCursor<EconomyProfile> cursor)
    {
        _profiles.FindSync(
            Arg.Any<FilterDefinition<EconomyProfile>>(),
            Arg.Any<FindOptions<EconomyProfile, EconomyProfile>>(),
            Arg.Any<CancellationToken>())
            .Returns(cursor);
        return new EconomyRepository(_profiles, _transactions);
    }

    private static IAsyncCursor<EconomyProfile> Cursor(params EconomyProfile[] profiles)
    {
        var cursor = Substitute.For<IAsyncCursor<EconomyProfile>>();
        var hasDocument = profiles.Length > 0;
        cursor.MoveNext(Arg.Any<CancellationToken>()).Returns(hasDocument);
        cursor.MoveNextAsync(Arg.Any<CancellationToken>()).Returns(hasDocument);
        cursor.Current.Returns(profiles);
        return cursor;
    }
}