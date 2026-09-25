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

public class BoasVindasSlashModuleTests
{
    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(BoasVindasSlashModule), CreateServices());
        return interactions;
    }

    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .BuildServiceProvider();

    private static string[] GetCommandPath(SlashCommandInfo command)
    {
        var segments = new List<string>();
        var module = command.Module;
        while (module is not null)
        {
            if (module.IsSlashGroup && !string.IsNullOrEmpty(module.SlashGroupName))
                segments.Insert(0, module.SlashGroupName);
            module = module.Parent;
        }
        segments.Add(command.Name);
        return segments.ToArray();
    }

    [Fact]
    public async Task BoasVindasSlashModule_ShouldRegister_UnderBoasVindasGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "boas-vindas status",
            "boas-vindas criar",
            "boas-vindas remover",
        });
    }

    [Fact]
    public async Task CriarAsync_ShouldExpose_NomeParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "criar");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "nome" && p.ParameterType == typeof(string) && p.IsRequired);
    }

    [Fact]
    public async Task RemoverAsync_ShouldExpose_NomeParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "remover");

        command.Parameters.Should().ContainSingle(p =>
            p.Name == "nome" && p.ParameterType == typeof(string) && p.IsRequired);
    }
}