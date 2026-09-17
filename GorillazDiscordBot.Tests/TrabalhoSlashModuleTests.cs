using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class TrabalhoSlashModuleTests
{
    [Fact]
    public async Task TrabalhoSlashModule_RegistraSubcomandosNoGrupoTrabalho()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(Substitute.For<IEconomyRepository>())
            .AddSingleton(new JobExamSessionService((pool, count) => pool.Take(count).ToList()))
            .AddSingleton(new ShopService(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>(),
                Substitute.For<ICharacterProfileRepository>()))
            .AddSingleton(Substitute.For<IEconomyAccessor>())
            .AddSingleton(new InflationService(Substitute.For<IEconomyRepository>()))
            .AddSingleton(new ManobristaSessionService())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(TrabalhoSlashModule), services);

        var commands = interactions.SlashCommands.ToList();

        commands.Should().HaveCount(4);
        commands.Should().OnlyContain(c => c.Module.IsSlashGroup && c.Module.SlashGroupName == "trabalho");
        commands.Select(c => c.Name).Should().BeEquivalentTo("listar", "trabalhar", "prova", "diplomas");
    }
}