using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class CrimeSlashModuleTests
{
    [Fact]
    public async Task CrimeSlashModule_ShouldRegister_AllSlashCommands()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<IEconomyRepository>())
            .AddSingleton(Substitute.For<IEconomyAccessor>())
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(Substitute.For<ShopService>(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>(),
                Substitute.For<ICharacterProfileRepository>()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(CrimeSlashModule), services);

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        var expected = new[]
        {
            "crime arsenal",
            "crime fianca",
            "crime furto",
            "crime roubar"
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
