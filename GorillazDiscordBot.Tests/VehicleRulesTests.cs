using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;

namespace GorillazDiscordBot.Tests;

public class VehicleRulesTests
{
    [Theory]
    [InlineData(VehicleType.Moto, LicenseLevel.A)]
    [InlineData(VehicleType.Carro, LicenseLevel.B)]
    [InlineData(VehicleType.Caminhonete, LicenseLevel.B)]
    [InlineData(VehicleType.Esportivo, LicenseLevel.B)]
    [InlineData(VehicleType.Caminhao, LicenseLevel.C)]
    [InlineData(VehicleType.Onibus, LicenseLevel.D)]
    [InlineData(VehicleType.Carreta, LicenseLevel.E)]
    [InlineData(VehicleType.Lancha, LicenseLevel.Arrais)]
    [InlineData(VehicleType.Iate, LicenseLevel.Mestre)]
    [InlineData(VehicleType.Navio, LicenseLevel.Capitao)]
    [InlineData(VehicleType.Aviao, LicenseLevel.PilotoPrivado)]
    [InlineData(VehicleType.Jato, LicenseLevel.PilotoLinhaAerea)]
    public void LicenseForType_MapaCorreto(VehicleType type, LicenseLevel expected)
    {
        VehicleRules.LicenseForType(type).Should().Be(expected);
    }

    [Fact]
    public void LicenseForType_None_RetornaNulo()
    {
        VehicleRules.LicenseForType(VehicleType.None).Should().BeNull();
    }

    private static ShopItem Vehicle(VehicleType type, LicenseLevel? license = null) => new()
    {
        Key = "x",
        Name = "x",
        Category = ItemCategory.Vehicle,
        VehicleType = type,
        RequiredLicense = license
    };

    [Fact]
    public void RequiredLicense_UsaCampoQuandoPresente()
    {
        var item = Vehicle(VehicleType.Carro, LicenseLevel.C);

        VehicleRules.RequiredLicense(item).Should().Be(LicenseLevel.C);
        VehicleRules.IsLicensedVehicle(item).Should().BeTrue();
    }

    [Fact]
    public void RequiredLicense_DerivaDoTipoQuandoCampoAusente()
    {
        var item = Vehicle(VehicleType.Jato);

        VehicleRules.RequiredLicense(item).Should().Be(LicenseLevel.PilotoLinhaAerea);
        VehicleRules.IsLicensedVehicle(item).Should().BeTrue();
    }

    [Fact]
    public void RequiredLicense_ItemNaoVeiculo_RetornaNulo()
    {
        var item = new ShopItem
        {
            Key = "relogio",
            Category = ItemCategory.Relic,
            VehicleType = VehicleType.Carro
        };

        VehicleRules.RequiredLicense(item).Should().BeNull();
        VehicleRules.IsLicensedVehicle(item).Should().BeFalse();
    }

    [Fact]
    public void RequiredLicense_Nulo_RetornaNulo()
    {
        VehicleRules.RequiredLicense(null).Should().BeNull();
    }

    [Fact]
    public void FormatRequirement_ExibeNomeDaCategoria()
    {
        VehicleRules.FormatRequirement(LicenseLevel.B)
            .Should().Contain("Categoria B");
    }
}
