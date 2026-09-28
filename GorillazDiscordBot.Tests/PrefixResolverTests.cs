using FluentAssertions;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Tests;

public class PrefixResolverTests
{
    private const string DefaultPrefix = "macaco ";

    [Fact]
    public void Resolve_SemPrefixoPersonalizado_RetornaPadraoGlobal()
    {
        PrefixResolver.Resolve(null, DefaultPrefix).Should().Be(DefaultPrefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_ComPrefixoNuloOuVazio_RetornaPadraoGlobal(string? prefix)
    {
        var result = PrefixResolver.Resolve(prefix, DefaultPrefix);

        result.Should().Be(DefaultPrefix);
    }

    [Fact]
    public void Resolve_ComPrefixoPersonalizado_RetornaPrefixo()
    {
        var result = PrefixResolver.Resolve("!", DefaultPrefix);

        result.Should().Be("!");
    }
}