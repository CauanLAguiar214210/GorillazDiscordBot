using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class CharacterProfileTests
{
    [Fact]
    public void NovoPerfil_ComValuesPadrao()
    {
        var profile = new CharacterProfile();

        profile.UserId.Should().Be(0);
        profile.Username.Should().BeEmpty();
        profile.Escolaridade.Should().Be(SchoolingLevel.Nenhuma);
        profile.Licencas.Should().BeEmpty();
        profile.Diplomas.Should().BeEmpty();
        profile.CasaAtualKey.Should().BeNull();
        profile.VeiculoAtualKey.Should().BeNull();
        profile.RoupaAtualKey.Should().BeNull();
    }
}