using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Config;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ConfigSlashModuleTests
{
    [Fact]
    public async Task ConfigSlashModule_ShouldRegister_UnderConfigGroup()
    {
        var services = new ServiceCollection()
            .AddSingleton<ISettingsRepository<Guild>>(Substitute.For<ISettingsRepository<Guild>>())
            .AddSingleton(Options.Create(new BotOptions()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ConfigSlashModule), services);

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        var expected = new[]
        {
            "config boasvindas-canal",
            "config boasvindas-desativar",
            "config boasvindas-exibir",
            "config boasvindas-mensagem",
            "config despedidas-canal",
            "config despedidas-desativar",
            "config despedidas-mensagem",
            "config prefixo-definir",
            "config prefixo-exibir",
            "config prefixo-resetar",
            "config status",
            "config voice-desativar",
            "config voice-exibir",
            "config voice-remover",
            "config voice-setup",
        };

        paths.Should().BeEquivalentTo(expected);
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