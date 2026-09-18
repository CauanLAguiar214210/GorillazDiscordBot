using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Moderation;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ModerationSlashModuleTests
{
    [Fact]
    public async Task ModerationSlashModule_ShouldRegister_UnderModeracaoGroup()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildMemberRepository>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ModerationSlashModule), services);

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        paths.Should().BeEquivalentTo(new[]
        {
            "moderacao avisar",
            "moderacao avisos",
            "moderacao banimentos",
            "moderacao banir",
            "moderacao desbanir",
            "moderacao destrancar",
            "moderacao expulsar",
            "moderacao limpar",
            "moderacao limpar-tudo",
            "moderacao removeaviso",
            "moderacao ritmolento",
            "moderacao timeout",
            "moderacao trancar",
        });
    }

    [Fact]
    public async Task LimparAsync_ShouldExpose_Quantidade_E_UsuarioParams()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildMemberRepository>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ModerationSlashModule), services);

        var limpar = interactions.SlashCommands
            .Single(c => c.Name == "limpar");

        limpar.Parameters.Should().HaveCount(2);

        limpar.Parameters[0].Name.Should().Be("quantidade");
        limpar.Parameters[0].ParameterType.Should().Be(typeof(int));
        limpar.Parameters[0].IsRequired.Should().BeFalse();

        limpar.Parameters[1].Name.Should().Be("usuário");
        limpar.Parameters[1].ParameterType.Should().Be(typeof(IUser));
        limpar.Parameters[1].IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task BanirAsync_ShouldExpose_Usuario_Motivo_E_DiasParams()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildMemberRepository>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ModerationSlashModule), services);

        var banir = interactions.SlashCommands
            .Single(c => c.Name == "banir");

        banir.Parameters.Should().HaveCount(3);

        banir.Parameters[0].Name.Should().Be("usuário");
        banir.Parameters[0].ParameterType.Should().Be(typeof(IUser));
        banir.Parameters[0].IsRequired.Should().BeTrue();

        banir.Parameters[1].Name.Should().Be("motivo");
        banir.Parameters[1].ParameterType.Should().Be(typeof(string));
        banir.Parameters[1].IsRequired.Should().BeFalse();

        banir.Parameters[2].Name.Should().Be("dias");
        banir.Parameters[2].ParameterType.Should().Be(typeof(int));
        banir.Parameters[2].IsRequired.Should().BeFalse();
    }

    [Fact]
    public async Task AvisosAsync_ShouldExpose_OptionalUsuario()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IGuildMemberRepository>())
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ModerationSlashModule), services);

        var avisos = interactions.SlashCommands
            .Single(c => c.Name == "avisos");

        avisos.Parameters.Should().HaveCount(1);
        avisos.Parameters[0].Name.Should().Be("usuário");
        avisos.Parameters[0].IsRequired.Should().BeFalse();
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