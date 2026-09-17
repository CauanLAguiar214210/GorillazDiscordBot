namespace GorillazDiscordBot.Domain.Entity.Profile;

public static class VehicleRules
{
    private static readonly IReadOnlyDictionary<string, LicenseLevel> RequiredLicenses =
        new Dictionary<string, LicenseLevel>
        {
            ["moto"] = LicenseLevel.A,
            ["carro_popular"] = LicenseLevel.B,
            ["caminhonete"] = LicenseLevel.B,
            ["carro_esportivo"] = LicenseLevel.B,
            ["caminhao"] = LicenseLevel.C,
            ["onibus"] = LicenseLevel.D,
            ["carreta"] = LicenseLevel.E,
            ["lancha"] = LicenseLevel.Arrais,
            ["iate"] = LicenseLevel.Mestre,
            ["navio"] = LicenseLevel.Capitao,
            ["aviao"] = LicenseLevel.PilotoPrivado,
            ["jato"] = LicenseLevel.PilotoLinhaAerea
        };

    public static LicenseLevel? RequiredLicense(string itemKey)
        => RequiredLicenses.TryGetValue(itemKey, out var level) ? level : null;

    public static bool IsLicensedVehicle(string itemKey)
        => RequiredLicenses.ContainsKey(itemKey);

    public static string FormatRequirement(LicenseLevel level)
        => $"{LicenseProgression.Info(level).Emoji} **{LicenseProgression.Info(level).Name}**";
}