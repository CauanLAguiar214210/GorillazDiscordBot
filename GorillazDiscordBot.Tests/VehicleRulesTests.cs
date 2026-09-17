using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class VehicleRulesTests
{
    [Theory]
    [InlineData("moto", LicenseLevel.A)]
    [InlineData("carro_popular", LicenseLevel.B)]
    [InlineData("caminhonete", LicenseLevel.B)]
    [InlineData("carro_esportivo", LicenseLevel.B)]
    [InlineData("caminhao", LicenseLevel.C)]
    [InlineData("onibus", LicenseLevel.D)]
    [InlineData("carreta", LicenseLevel.E)]
    [InlineData("lancha", LicenseLevel.Arrais)]
    [InlineData("iate", LicenseLevel.Mestre)]
    [InlineData("navio", LicenseLevel.Capitao)]
    [InlineData("aviao", LicenseLevel.PilotoPrivado)]
    [InlineData("jato", LicenseLevel.PilotoLinhaAerea)]
    public void RequiredLicense_MapaCorreto(string key, LicenseLevel expected)
    {
        VehicleRules.RequiredLicense(key).Should().Be(expected);
        VehicleRules.IsLicensedVehicle(key).Should().BeTrue();
    }

    [Fact]
    public void RequiredLicense_ChaveDesconhecida_RetornaNulo()
    {
        VehicleRules.RequiredLicense("relogio").Should().BeNull();
        VehicleRules.IsLicensedVehicle("relogio").Should().BeFalse();
    }

    [Fact]
    public void FormatRequirement_ExibeNomeDaCategoria()
    {
        VehicleRules.FormatRequirement(LicenseLevel.B)
            .Should().Contain("Categoria B");
    }
}