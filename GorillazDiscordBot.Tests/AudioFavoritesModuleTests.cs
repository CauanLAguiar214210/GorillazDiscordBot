using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Commands.Audio;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Tests;

public class AudioFavoritesModuleTests
{
    private static IServiceProvider CreateServices() => new ServiceCollection()
        .AddSingleton(Substitute.For<IAudioPlayerService>())
        .AddSingleton(Substitute.For<IPersistentVoiceService>())
        .AddSingleton(Substitute.For<IAudioUploadService>())
        .AddSingleton(Substitute.For<ISettingsRepository<Guild>>())
        .AddSingleton<IOptions<AudioUploadOptions>>(Options.Create(new AudioUploadOptions { Path = "test-uploads" }))
        .BuildServiceProvider();

    private static async Task<InteractionService> CreateInteractionsAsync()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(AudioSlashModule), CreateServices());
        return interactions;
    }

    private static async Task<CommandService> CreateCommandsAsync()
    {
        var commands = new CommandService();
        await commands.AddModuleAsync(typeof(AudioPrefixModule), CreateServices());
        return commands;
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
    public async Task SlashModule_RegistraOsComandosDeFavoritos()
    {
        var interactions = await CreateInteractionsAsync();

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .ToArray();

        paths.Should().Contain("audio listar");
        paths.Should().NotContain([
            "audio favs",
            "audio favs listar",
            "audio favs favoritar",
            "audio favs favoritar-anexo",
            "audio favs desfavoritar",
            "audio favs tocar-fav",
            "audio favoritar",
            "audio favoritar-anexo",
            "audio desfavoritar",
            "audio tocar-fav"]);
        paths.Should().NotContain("audio sons");
    }

    [Fact]
    public async Task SlashModule_NaoRegistraComandoSeparadoDeFavoritar()
    {
        var interactions = await CreateInteractionsAsync();

        interactions.SlashCommands.Should().NotContain(c => c.Name == "favoritar");
    }

    [Fact]
    public async Task SlashModule_NaoRegistraComandoSeparadoDeTocarFav()
    {
        var interactions = await CreateInteractionsAsync();

        interactions.SlashCommands.Should().NotContain(c => c.Name == "tocar-fav");
    }

    [Fact]
    public async Task PrefixModule_RegistraOsComandosDeFavoritos()
    {
        var commands = await CreateCommandsAsync();

        var names = commands.Commands.Select(c => c.Name).ToArray();

        names.Should().NotContain(["favs", "favoritar", "favoritar-anexo", "desfavoritar", "tocarfav"]);
    }

    [Fact]
    public async Task PrefixModule_NaoRegistraFavoritarSeparado()
    {
        var commands = await CreateCommandsAsync();
        commands.Commands.Should().NotContain(c => c.Name == "favoritar");
    }

    [Fact]
    public async Task PrefixModule_NaoRegistraFavoritarAnexoSeparado()
    {
        var commands = await CreateCommandsAsync();
        commands.Commands.Should().NotContain(c => c.Name == "favoritar-anexo");
    }
}
