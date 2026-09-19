using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Release;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ReleaseSlashModuleTests
{
    [Fact]
    public async Task ReleaseSlashModule_ShouldRegister_UnderReleaseGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "release anunciar",
            "release listar",
        });
    }

    [Fact]
    public async Task ListarCommand_DeveTerOpcaoTodas_Opcional()
    {
        var interactions = await CreateInteractionsAsync();

        var listar = interactions.SlashCommands.Single(c => c.Name == "listar");
        var option = listar.Parameters.Should().ContainSingle().Subject;

        option.Name.Should().Be("todas");
        option.IsRequired.Should().BeFalse();
    }

    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var services = new ServiceCollection()
            .AddSingleton<IReleaseNoteRepository>(Substitute.For<IReleaseNoteRepository>())
            .AddSingleton(new ReleaseAnnouncementService(
                Substitute.For<ISettingsRepository<Guild>>(),
                Substitute.For<IReleaseNoteRepository>(),
                NullLogger<ReleaseAnnouncementService>.Instance))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ReleaseSlashModule), services);
        return interactions;
    }

    private static string[] GetCommandPath(SlashCommandInfo command)
    {
        var segments = new List<string>();

        var module = command.Module;
        while (module is not null)
        {
            if (module.IsSlashGroup)
                segments.Insert(0, module.SlashGroupName);
            module = module.Parent;
        }

        segments.Add(command.Name);
        return segments.ToArray();
    }
}
