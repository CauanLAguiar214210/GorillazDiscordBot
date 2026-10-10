namespace GorillazDiscordBot.Entity;

public sealed class ModuleSettings
{
    // A ausência de uma chave mantém o módulo ativo para compatibilidade com guildas existentes.
    public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? PlanId { get; set; }
    public string? LicenseStatus { get; set; }
    public DateTime? LicenseExpiresAt { get; set; }
}
