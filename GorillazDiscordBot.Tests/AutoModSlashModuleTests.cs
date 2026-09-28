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

public class AutoModSlashModuleTests
{
    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .BuildServiceProvider();

    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(AutoModSlashModule), CreateServices());
        return interactions;
    }

    [Fact]
    public async Task AutoModSlashModule_ShouldRegister_UnderAutomodGroup()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "automod acao",
            "automod ativar",
            "automod limite",
            "automod palavra-adicionar",
            "automod palavra-remover",
            "automod palavras",
            "automod status",
            "automod timeout",
        });
    }

    [Fact]
    public async Task PalavraAdicionarAsync_ShouldExpose_PalavraParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "palavra-adicionar");

        command.Parameters.Should().ContainSingle(p => p.Name == "palavra" && p.IsRequired && p.ParameterType == typeof(string));
    }

    [Fact]
    public async Task LimiteAsync_ShouldExpose_MensagensESegundosParams()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "limite");

        command.Parameters.Should().HaveCount(2);
        command.Parameters[0].Name.Should().Be("mensagens");
        command.Parameters[0].ParameterType.Should().Be(typeof(int));
        command.Parameters[0].IsRequired.Should().BeTrue();

        command.Parameters[1].Name.Should().Be("segundos");
        command.Parameters[1].ParameterType.Should().Be(typeof(int));
        command.Parameters[1].IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task AcaoAsync_ShouldExpose_EnumAcaoParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "acao");

        command.Parameters.Should().ContainSingle(p => p.Name == "acao" && p.ParameterType == typeof(AutomodAction));
    }

    [Fact]
    public async Task TimeoutAsync_ShouldExpose_MinutosParam()
    {
        var interactions = await CreateInteractionsAsync();

        var command = interactions.SlashCommands.Single(c => c.Name == "timeout");

        command.Parameters.Should().ContainSingle(p => p.Name == "minutos" && p.ParameterType == typeof(int) && p.IsRequired);
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