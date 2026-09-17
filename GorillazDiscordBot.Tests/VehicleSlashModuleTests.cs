using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Vehicle;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class VehicleSlashModuleTests
{
    [Fact]
    public async Task VehicleModules_RegistramVeiculoLicencaEGaragem()
    {
        var services = new ServiceCollection()
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(Substitute.For<IEconomyRepository>())
            .AddSingleton(Substitute.For<IEconomyAccessor>())
            .AddSingleton(new LicencaExamSessionService((pool, count) => pool.Take(count).ToList()))
            .AddSingleton(new ShopService(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IEconomyAccessor>(),
                Substitute.For<ICharacterProfileRepository>()))
            .BuildServiceProvider();

        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(VehicleSlashModule), services);
        await interactions.AddModuleAsync(typeof(GaragemSlashModule), services);

        var commands = interactions.SlashCommands.ToList();

        commands.Select(c => c.Name).Should().Contain(new[] { "ver", "prova", "dirigir", "estacionar", "atual", "garagem" });

        commands.Where(c => c.Name is "ver" or "prova")
            .Should().OnlyContain(c => c.Module.IsSlashGroup && c.Module.SlashGroupName == "licenca");

        commands.Where(c => c.Name is "dirigir" or "estacionar" or "atual")
            .Should().OnlyContain(c => c.Module.IsSlashGroup && c.Module.SlashGroupName == "veiculo");

        commands.Single(c => c.Name == "garagem")
            .Module.IsSlashGroup.Should().BeFalse();
    }
}