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

public class PermissionSlashModuleTests
{
    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .BuildServiceProvider();

    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(PermissionSlashModule), CreateServices());
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
    public async Task PermissionSlashModule_ShouldRegister_UnderPermGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "permissao ativar",
            "permissao conceder",
            "permissao revogar",
            "permissao status",
        });
    }

    [Fact]
    public async Task ConcederAsync_ShouldExpose_ComandoECargoParams()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "conceder");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "comando" && p.ParameterType == typeof(string) && p.IsRequired);
        command.Parameters.Should().ContainSingle(p =>
            p.Name == "cargo" && p.ParameterType == typeof(IRole) && p.IsRequired);
    }

    [Fact]
    public async Task RevogarAsync_ShouldExpose_ComandoECargoParams()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "revogar");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "comando" && p.ParameterType == typeof(string) && p.IsRequired);
        command.Parameters.Should().ContainSingle(p =>
            p.Name == "cargo" && p.ParameterType == typeof(IRole) && p.IsRequired);
    }
}