using FluentAssertions;
using GorillazDiscordBot.Infra.Configuration;

namespace GorillazDiscordBot.Tests;

public class MongoMappingsTests
{
    [Fact]
    public void Register_ChamadoDuasVezes_NaoLancaExcecao()
    {
        MongoMappings.Register();

        var act = MongoMappings.Register;

        act.Should().NotThrow();
    }
}
