using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Moderation;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ModLogSlashModuleTests
{
    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .BuildServiceProvider();

    private static async Task<InteractionService> CreateInteractionServiceAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ModLogSlashModule), CreateServices());
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

    [Fact]
    public async Task ModLogSlashModule_ShouldRegister_UnderModlogGroup()
    {
        var interactions = await CreateInteractionServiceAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "modlog ativar",
            "modlog canal",
            "modlog eventos",
            "modlog status",
        });
    }

    [Fact]
    public async Task CanalAsync_ShouldExpose_CanalParam()
    {
        var interactions = await CreateInteractionServiceAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "canal");

        command.Parameters.Should().ContainSingle(
            p => p.Name == "canal" && p.IsRequired && p.ParameterType == typeof(ITextChannel));
    }
}