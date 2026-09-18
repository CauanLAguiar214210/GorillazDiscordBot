using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Interaction;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class InteractionSlashModuleTests
{
    [Fact]
    public async Task InteractionSlashModule_ShouldRegister_UnderInteracaoGroup()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildInteractionRepository>())
            .AddSingleton(Substitute.For<IGifUrlService>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(InteractionSlashModule), services);

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "interacao criar",
            "interacao listar",
            "interacao remover",
        });
    }

    [Fact]
    public async Task CriarAsync_ShouldExpose_Trigger_Resposta_E_TipoParams()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildInteractionRepository>())
            .AddSingleton(Substitute.For<IGifUrlService>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(InteractionSlashModule), services);

        var criar = interactions.SlashCommands
            .Single(c => c.Name == "criar");

        criar.Parameters.Should().HaveCount(3);

        criar.Parameters[0].Name.Should().Be("trigger");
        criar.Parameters[0].ParameterType.Should().Be(typeof(string));

        criar.Parameters[1].Name.Should().Be("resposta");
        criar.Parameters[1].ParameterType.Should().Be(typeof(string));

        criar.Parameters[2].Name.Should().Be("tipo");
        criar.Parameters[2].ParameterType.Should().Be(typeof(GuildInteractionType));
        criar.Parameters[2].IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task ListarAsync_ShouldNotExposeParams()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildInteractionRepository>())
            .AddSingleton(Substitute.For<IGifUrlService>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(InteractionSlashModule), services);

        var listar = interactions.SlashCommands
            .Single(c => c.Name == "listar");

        listar.Parameters.Should().BeEmpty();
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