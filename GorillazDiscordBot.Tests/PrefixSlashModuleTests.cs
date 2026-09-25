using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Moderation;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class PrefixSlashModuleTests
{
    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(PrefixSlashModule), CreateServices());
        return interactions;
    }

    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .AddSingleton(Options.Create(new BotOptions { CommandPrefix = "macaco " }))
        .BuildServiceProvider();

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
    public async Task PrefixSlashModule_ShouldRegister_UnderPrefixGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "prefixo status",
            "prefixo definir",
            "prefixo remover",
        });
    }

    [Fact]
    public async Task DefinirAsync_ShouldExpose_PrefixoParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "definir");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "prefixo" && p.ParameterType == typeof(string) && p.IsRequired);
    }
}