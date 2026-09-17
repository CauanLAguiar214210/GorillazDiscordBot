using GorillazDiscordBot.Domain.Entity.Economy;

namespace GorillazDiscordBot.Domain.Entity.Profile;

public static class VehicleRules
{
    private static readonly IReadOnlyDictionary<VehicleType, LicenseLevel> LicensesByType =
        new Dictionary<VehicleType, LicenseLevel>
        {
            [VehicleType.Moto] = LicenseLevel.A,
            [VehicleType.Carro] = LicenseLevel.B,
            [VehicleType.Caminhonete] = LicenseLevel.B,
            [VehicleType.Esportivo] = LicenseLevel.B,
            [VehicleType.Caminhao] = LicenseLevel.C,
            [VehicleType.Onibus] = LicenseLevel.D,
            [VehicleType.Carreta] = LicenseLevel.E,
            [VehicleType.Lancha] = LicenseLevel.Arrais,
            [VehicleType.Iate] = LicenseLevel.Mestre,
            [VehicleType.Navio] = LicenseLevel.Capitao,
            [VehicleType.Aviao] = LicenseLevel.PilotoPrivado,
            [VehicleType.Jato] = LicenseLevel.PilotoLinhaAerea
        };

    public static LicenseLevel? LicenseForType(VehicleType type)
        => LicensesByType.TryGetValue(type, out var level) ? level : null;

    public static LicenseLevel? RequiredLicense(ShopItem? item)
    {
        if (item is not { Category: ItemCategory.Vehicle })
            return null;

        return item.RequiredLicense ?? LicenseForType(item.VehicleType);
    }

    public static bool IsLicensedVehicle(ShopItem? item)
        => RequiredLicense(item) is not null;

    public static string FormatRequirement(LicenseLevel level)
        => $"{LicenseProgression.Info(level).Emoji} **{LicenseProgression.Info(level).Name}**";
}
