using Discord.Interactions;
using Discord.WebSocket;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace GorillazDiscordBot.Tests;

public class GroupBehaviorTests
{
    [Group("testegrupo", "Teste")]
    public class TestModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("sub", "sub")]
        public async Task SubAsync() => await Task.CompletedTask;
    }

    [Fact]
    public async Task ModuleLevelGroup_ShouldPrefix_CommandFullPath()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var interactions = new InteractionService(new DiscordSocketClient());

        await interactions.AddModuleAsync(typeof(TestModule), services);

        var command = interactions.SlashCommands.Should().ContainSingle().Subject;

        command.Module.IsSlashGroup.Should().BeTrue();
        command.Module.SlashGroupName.Should().Be("testegrupo");
        command.Name.Should().Be("sub");
    }
}