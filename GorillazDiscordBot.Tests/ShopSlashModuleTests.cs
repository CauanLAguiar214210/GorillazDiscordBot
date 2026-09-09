using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Shop;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class ShopSlashModuleTests
{
    [Fact]
    public async Task ShopSlashModule_ShouldRegister_AllSlashCommands()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ShopService>(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ShopSlashModule), services);

        var paths = interactions.SlashCommands
            .Select(GetCommandPath)
            .Select(p => string.Join(" ", p))
            .OrderBy(p => p)
            .ToArray();

        var expected = new[]
        {
            "comprar",
            "desequipar",
            "equipar",
            "inventario",
            "loja",
            "usar",
            "vender",
        };

        paths.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShopSlashModule_ShouldRegister_InventoryComponentHandlers()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ShopService>(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(ShopSlashModule), services);

        var names = interactions.ComponentCommands
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToArray();

        var expected = new[]
        {
            "inv:equip:*:*",
            "inv:sel:*",
            "inv:sell:*:*",
            "inv:use:*:*",
            "shop:back:*:*",
            "shop:buy:*:*",
            "shop:cat:*",
            "shop:item:*:*",
        };

        names.Should().BeEquivalentTo(expected);
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
