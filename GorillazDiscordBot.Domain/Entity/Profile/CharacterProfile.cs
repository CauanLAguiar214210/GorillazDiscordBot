namespace GorillazDiscordBot.Domain.Entity.Profile;

public enum SchoolingLevel
{
    Nenhuma,
    EnsinoFundamental1,
    EnsinoFundamental2,
    EnsinoMedio,
    EnsinoSuperior
}

public enum LicenseDomain
{
    Terrestre,
    Maritima,
    Aerea
}

public enum LicenseLevel
{
    A,
    B,
    C,
    D,
    E,
    Arrais,
    Mestre,
    Capitao,
    PilotoPrivado,
    PilotoComercial,
    PilotoLinhaAerea
}

public class CharacterProfile
{
    public string Id { get; set; } = string.Empty;
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public SchoolingLevel Escolaridade { get; set; }
    public List<LicenseLevel> Licencas { get; set; } = new();
    public List<string> Diplomas { get; set; } = new();
    public string? CasaAtualKey { get; set; }
    public string? VeiculoAtualKey { get; set; }
    public string? RoupaAtualKey { get; set; }
    public string? ArmaAtualKey { get; set; }
    public string? EquipamentoAtualKey { get; set; }
    public string? ProfissaoKey { get; set; }
    public string? ExtraKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}