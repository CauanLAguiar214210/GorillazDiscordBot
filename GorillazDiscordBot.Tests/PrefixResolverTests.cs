using FluentAssertions;
using GorillazDiscordBot.Commands;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class PrefixResolverTests
{
    private const string DefaultPrefix = "macaco ";

    [Fact]
    public void GetCurrentPrefix_SemPrefixoPersonalizado_RetornaPadraoGlobal()
    {
        var module = CreateModule();

        var result = module.GetCurrentPrefix(new GuildPrefixSettings { GuildId = 1 });

        result.Should().Be(DefaultPrefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCurrentPrefix_ComPrefixoNuloOuVazio_RetornaPadraoGlobal(string? prefix)
    {
        var module = CreateModule();

        var result = module.GetCurrentPrefix(new GuildPrefixSettings { GuildId = 1, Prefix = prefix });

        result.Should().Be(DefaultPrefix);
    }

    [Fact]
    public void GetCurrentPrefix_ComPrefixoPersonalizado_RetornaPrefixo()
    {
        var module = CreateModule();

        var result = module.GetCurrentPrefix(new GuildPrefixSettings { GuildId = 1, Prefix = "!" });

        result.Should().Be("!");
    }

    private static PrefixModule CreateModule()
    {
        var repository = Substitute.For<ISettingsRepository<GuildPrefixSettings>>();
        var options = Options.Create(new BotOptions { CommandPrefix = DefaultPrefix });
        return new PrefixModule(repository, options);
    }
}
