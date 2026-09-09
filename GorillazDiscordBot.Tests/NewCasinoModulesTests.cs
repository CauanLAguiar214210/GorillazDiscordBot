using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Commands.Casino;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class NewCasinoModulesTests
{
    private readonly IServiceProvider _services;

    public NewCasinoModulesTests()
    {
        _services = new ServiceCollection()
            .AddSingleton(Substitute.For<CasinoPlayService>(
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>(),
                Substitute.For<ShopService>(
                    Substitute.For<IShopRepository>(),
                    Substitute.For<IEconomyRepository>(),
                    Substitute.For<IEconomyAccessor>())))
            .AddSingleton(new CasinoSessionManager())
            .BuildServiceProvider();
    }

    [Fact]
    public async Task DiceSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(DiceSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("dados");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "dic:leave:*",
            "dic:paytable",
            "dic:replay:*:*:*",
            "dic:roll"
        });
    }

    [Fact]
    public async Task CoinFlipSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(CoinFlipSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("caraoucoroa");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "coin:flip",
            "coin:leave:*",
            "coin:paytable",
            "coin:replay:*:*:*"
        });
    }

    [Fact]
    public async Task AviaoSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(AviaoSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("aviaozinho");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "aviao:fly",
            "aviao:leave:*",
            "aviao:paytable",
            "aviao:peg",
            "aviao:replay:*:*"
        });
    }

    [Fact]
    public async Task VideoPokerSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(VideoPokerSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("poker");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "poker:draw",
            "poker:hold:*",
            "poker:leave:*",
            "poker:paytable",
            "poker:replay:*:*"
        });
    }

    [Fact]
    public async Task MinesSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(MinesSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("minas");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "mines:leave:*",
            "mines:paytable",
            "mines:replay:*:*:*",
            "mines:reveal:*",
            "mines:sacar"
        });
    }

    [Fact]
    public async Task LimboSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(LimboSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("limbo");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "limbo:leave:*",
            "limbo:paytable",
            "limbo:replay:*:*:*",
            "limbo:revelar"
        });
    }

    [Fact]
    public async Task RpsSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(RpsSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("jokenpo");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "rps:jogar",
            "rps:leave:*",
            "rps:paytable",
            "rps:replay:*:*:*"
        });
    }

    [Fact]
    public async Task RaceSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(RaceSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("corrida");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "race:largada",
            "race:leave:*",
            "race:paytable",
            "race:replay:*:*:*"
        });
    }

    [Fact]
    public async Task PlinkoSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(PlinkoSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("plinko");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "plinko:largar",
            "plinko:leave:*",
            "plinko:paytable",
            "plinko:replay:*:*"
        });
    }

    [Fact]
    public async Task WheelSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(WheelSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("roda");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "roda:girar",
            "roda:leave:*",
            "roda:paytable",
            "roda:replay:*:*"
        });
    }

    [Fact]
    public async Task HighLowSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(HighLowSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("altobaixo");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "hl:leave:*",
            "hl:maior",
            "hl:menor",
            "hl:paytable",
            "hl:replay:*:*",
            "hl:sacar"
        });
    }

    [Fact]
    public async Task BaccaratSlashModule_ShouldRegister_SlashAndComponents()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(BaccaratSlashModule), _services);

        interactions.SlashCommands.Select(c => c.Name).Should().Contain("baccarat");

        var names = interactions.ComponentCommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(new[]
        {
            "bac:leave:*",
            "bac:paytable",
            "bac:replay:*:*:*",
            "bac:revelar"
        });
    }
}