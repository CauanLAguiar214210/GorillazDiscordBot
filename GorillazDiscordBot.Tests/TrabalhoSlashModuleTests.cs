using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using GorillazDiscordBot.Api.Commands.Economy;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class TrabalhoSlashModuleTests
{
    [Fact]
    public async Task TrabalhoSlashModule_RegistraSubcomandosNoGrupoTrabalho()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(TrabalhoSlashModule), BuildServices());

        var commands = interactions.SlashCommands.ToList();

        commands.Should().HaveCount(4);
        commands.Should().OnlyContain(c => c.Module.IsSlashGroup && c.Module.SlashGroupName == "trabalho");
        commands.Select(c => c.Name).Should().BeEquivalentTo("listar", "trabalhar", "profissao", "extra");
    }

    [Fact]
    public async Task TrabalhoSlashModule_RegistraBotoesDeDefinirProfissaoEExtra()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(TrabalhoSlashModule), BuildServices());

        var names = interactions.ComponentCommands.Select(c => c.Name).ToArray();

        names.Should().Contain("trabalho:setpro:*:*");
        names.Should().Contain("trabalho:setextra:*:*");
    }

    [Fact]
    public async Task TrabalhoSlashModule_RegistraBotaoDeVoltarDedicado()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(TrabalhoSlashModule), BuildServices());

        var names = interactions.ComponentCommands.Select(c => c.Name).ToArray();

        names.Should().Contain("trabalho:back:*:*");
        names.Should().Contain("trabalho:job:*:*");
    }

    [Fact]
    public async Task TrabalhoSlashModule_ProfissaoEExtra_RegistramSemParametros()
    {
        var interactions = new InteractionService(new DiscordSocketClient());
        await interactions.AddModuleAsync(typeof(TrabalhoSlashModule), BuildServices());

        var profissao = interactions.SlashCommands.Single(c => c.Name == "profissao");
        var extra = interactions.SlashCommands.Single(c => c.Name == "extra");

        profissao.Parameters.Should().BeEmpty();
        extra.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void JobGameSessao_RemoveDeveUsarMesmaChaveDeInicio_MainIdNaoContextUser()
    {
        var games = new JobGameSessionService();

        const ulong contextUserId = 101;
        const ulong mainId = 999;

        games.TryStart(mainId, JobGameKind.Porteiro, out var session).Should().BeTrue();
        session.UserId.Should().Be(mainId);

        games.Remove(contextUserId);
        games.Get(mainId).Should().NotBeNull("a sessão vive na chave mainId, não na do contexto");

        games.Remove(mainId);
        games.Get(mainId).Should().BeNull();
    }

    private static ServiceProvider BuildServices()
        => new ServiceCollection()
            .AddSingleton(Substitute.For<ICharacterProfileRepository>())
            .AddSingleton(Substitute.For<IEconomyRepository>())
            .AddSingleton(new JobExamSessionService((pool, count) => pool.Take(count).ToList()))
            .AddSingleton(new ShopService(
                Substitute.For<IShopRepository>(),
                Substitute.For<IEconomyRepository>(),
                Substitute.For<IPrimaryAccountResolver>(),
                Substitute.For<ICharacterProfileRepository>()))
            .AddSingleton(Substitute.For<IPrimaryAccountResolver>())
            .AddSingleton(new ManobristaSessionService())
            .AddSingleton(new JobGameSessionService())
            .BuildServiceProvider();
}
