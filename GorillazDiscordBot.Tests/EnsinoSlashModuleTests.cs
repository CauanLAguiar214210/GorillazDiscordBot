using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class EnsinoSlashModuleTests
{
    [Fact]
    public async Task EnsinoSlashModule_RegistraProvaEVerNoGrupoEnsino()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(new QuizSessionService((pool, count) => pool.Take(count).ToList()))
            .AddSingleton(new JobExamSessionService((pool, count) => pool.Take(count).ToList()))
            .AddSingleton(Substitute.For<IEconomyAccessor>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(EnsinoSlashModule), services);

        var commands = interactions.SlashCommands.ToList();

        commands.Should().HaveCount(3);
        commands.Should().OnlyContain(c => c.Module.IsSlashGroup && c.Module.SlashGroupName == "ensino");
        commands.Select(c => c.Name).Should().BeEquivalentTo("ver", "prova", "faculdade");
    }
}