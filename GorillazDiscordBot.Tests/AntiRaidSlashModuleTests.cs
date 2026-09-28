using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Moderation;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class AntiRaidSlashModuleTests
{
    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .BuildServiceProvider();

    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(AntiRaidSlashModule), CreateServices());
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
    public async Task AntiRaidSlashModule_ShouldRegister_UnderAntiRaidGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "anti-raid ativar",
            "anti-raid limite",
            "anti-raid status",
        });
    }

    [Fact]
    public async Task AtivarAsync_ShouldExpose_AtivoParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "ativar");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "ativo" && p.ParameterType == typeof(bool) && p.IsRequired);
    }

    [Fact]
    public async Task LimiteAsync_ShouldExpose_EntradasESegundosParams()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "limite");

        command.Parameters.Should().HaveCount(2);
        command.Parameters[0].Name.Should().Be("entradas");
        command.Parameters[0].ParameterType.Should().Be(typeof(int));
        command.Parameters[0].IsRequired.Should().BeTrue();

        command.Parameters[1].Name.Should().Be("segundos");
        command.Parameters[1].ParameterType.Should().Be(typeof(int));
        command.Parameters[1].IsRequired.Should().BeFalse();
    }
}