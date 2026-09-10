namespace GorillazDiscordBot.Domain.Entity.Economy;

public static class PetFormatter
{
    public static string EffectTarget(UpgradeEffect effect) => effect switch
    {
        UpgradeEffect.Daily => "no daily",
        UpgradeEffect.Work => "no trabalho",
        UpgradeEffect.Rob => "nos roubos",
        UpgradeEffect.AssetIncome => "na renda dos ativos",
        UpgradeEffect.Savings => "nos juros da poupança",
        UpgradeEffect.RobDefense => "na defesa anti-roubo",
        UpgradeEffect.Casino => "nos ganhos de cassino",
        _ => "?"
    };

    public static int ValuePerLevel(ShopItem pet, int level)
        => pet is { EvolvedName: not null } && level >= pet.MaxQuantity
            ? pet.EvolvedUpgradeValue
            : pet.UpgradeValue;

    public static string TotalBonus(ShopItem pet, int level)
        => FormatPercent(ValuePerLevel(pet, level) * level, pet.UpgradeEffect);

    private static string FormatPercent(int value, UpgradeEffect effect)
        => effect == UpgradeEffect.RobDefense ? $"−{value}%" : $"+{value}%";

    public static string DescribePet(ShopItem pet)
    {
        var target = EffectTarget(pet.UpgradeEffect);
        var max = pet.MaxQuantity > 0 ? $" · máx. nível {pet.MaxQuantity}" : string.Empty;
        var evo = pet.EvolvedName is not null && pet.EvolvedUpgradeValue > 0
            ? $" · evolui p/ {pet.EvolvedEmoji} {pet.EvolvedName} (+{pet.EvolvedUpgradeValue}% por nível)"
            : string.Empty;
        return $"🐾 **{FormatPercent(pet.UpgradeValue, pet.UpgradeEffect)}** {target} por nível{max}{evo}";
    }

    public static string PetName(ShopItem pet, InventoryItem inv)
        => inv.PetNickname is not null
            ? $"{pet.Emoji} **{inv.PetNickname}** *({pet.Name})*"
            : $"{pet.Emoji} **{pet.Name}**";

    public static string LevelBar(int level, int max)
    {
        const int cells = 10;
        var ratio = max <= 0 ? 0.0 : (double)Math.Min(level, max) / max;
        var filled = Math.Clamp((int)Math.Round(ratio * cells), 0, cells);
        return new string('▰', filled) + new string('▱', cells - filled);
    }

    public static string ProgressLine(ShopItem pet, int level)
    {
        var max = pet.MaxQuantity > 0 ? pet.MaxQuantity : 1;
        var status = level >= max
            ? $"Nível **{level}/{max}** {LevelBar(level, max)} ✨ **EVOLUÍDO!**"
            : $"Nível **{level}/{max}** {LevelBar(level, max)}";
        return $"{status} · bônus total **{TotalBonus(pet, level)}**";
    }

    public static string EvolutionLine(ShopItem pet, int level)
    {
        if (pet.EvolvedName is null)
            return string.Empty;

        return level >= pet.MaxQuantity
            ? "✨ **Evoluído!** O bônus por nível foi dobrado."
            : $"🔒 Evolui no nível **{pet.MaxQuantity}** → **{pet.EvolvedEmoji} {pet.EvolvedName}** (+{pet.EvolvedUpgradeValue}% por nível)";
    }
}