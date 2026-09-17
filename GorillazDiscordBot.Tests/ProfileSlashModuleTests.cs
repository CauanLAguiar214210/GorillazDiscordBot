using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ProfileSlashModuleTests
{
    [Fact]
    public async Task ProfileSlashModule_ShouldRegister_VerSubcommandUnder_PerfilGroup()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(Substitute.For<IPatrimonioService>())
            .AddSingleton(Substitute.For<IEconomyAccessor>())
            .AddSingleton(new ShopService(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>(),
                Substitute.For<ICharacterProfileRepository>()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ProfileSlashModule), services);

        var command = interactions.SlashCommands.Should().ContainSingle().Subject;

        command.Module.IsSlashGroup.Should().BeTrue();
        command.Module.SlashGroupName.Should().Be("perfil");
        command.Name.Should().Be("ver");
    }
}