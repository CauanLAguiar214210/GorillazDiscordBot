using System.Text;
using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Entity.Ranking;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Profile;

[Group("perfil", "A carteira do seu personagem no jogo")]
public class ProfileSlashModule : InteractionModuleBase<SocketInteractionContext>
{
private readonly ICharacterProfileRepository _profiles;
    private readonly IPatrimonioService _patrimonio;
    private readonly IEconomyAccessor _accessor;
    private readonly ShopService _shop;

    public ProfileSlashModule(
        ICharacterProfileRepository profiles,
        IPatrimonioService patrimonio,
        IEconomyAccessor accessor,
        ShopService shop)
    {
        _profiles = profiles;
        _patrimonio = patrimonio;
        _accessor = accessor;
        _shop = shop;
    }

    [SlashCommand("ver", "Mostra as informações de um personagem (sem argumento, mostra o seu)")]
    public async Task VerAsync(Discord.IUser? usuario = null)
    {
        var target = usuario ?? Context.User;
        var mainId = await _accessor.ResolveMainIdAsync(target.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, target.Username);
        var snapshot = await _patrimonio.GetSnapshotAsync(mainId, target.Username);
        var classe = ClasseEconomica.Find(snapshot.Total);
        var fame = await _patrimonio.GetHallOfFameAsync(target.Id);
        var (vehicle, _) = await _shop.GetCurrentVehicleAsync(target.Id);

        var embed = BuildProfileEmbed(target, profile, snapshot, classe, fame, vehicle);
        await RespondAsync(embed: embed);
    }

    private static Embed BuildProfileEmbed(
        IUser user,
        CharacterProfile profile,
        PatrimonioSnapshot snapshot,
        ClasseEconomica classe,
        HallOfFame? fame,
        ShopItem? vehicle)
    {
        var sb = new StringBuilder();
        if (fame != null)
        {
            sb.AppendLine($"👑 **Hall da Fama:** {(FormatFame(fame))}");
            sb.AppendLine();
        }

        sb.AppendLine($"🎓 **Escolaridade:** {FormatSchooling(profile.Escolaridade)}");
        sb.AppendLine();
        sb.AppendLine($"📜 **Diplomas:** \n {(profile.Diplomas.Count > 0 ? string.Join(" · ", profile.Diplomas.Select(d => $"`{d}`")) : "Nenhum")}");
        sb.AppendLine();

        var profissao = EconomyJobs.FindByKey(profile.ProfissaoKey ?? string.Empty);
        sb.AppendLine($"💼 **Profissão:** {(profissao != null ? $"{profissao.Emoji} **{profissao.Name}**" : "Nenhuma")}");

        if (profile.ExtraKey is { } extraKey && EconomyJobs.FindByKey(extraKey) is { } extra)
            sb.AppendLine($"🧩 **Extra:** {extra.Emoji} **{extra.Name}**");
        sb.AppendLine();

        sb.AppendLine($"🚗 **Habilitação:** \n {(profile.Licencas.Count > 0 ? string.Join("\n ", profile.Licencas.Order().Select(FormatLicenca)) : "Nenhuma")}");
        sb.AppendLine($"🚙 **Veículo atual:** {(vehicle != null ? $"**{vehicle.Name}**" : "Nenhum (a pé)")}");
        sb.AppendLine();

        sb.AppendLine($"⛰️ **Classe:** {classe.Emoji} **{classe.Title}**");
        sb.AppendLine($"💎 **Patrimônio:** {EconomyFormat.Compact(snapshot.Total)} moedas (Inventario: {EconomyFormat.Compact(snapshot.ItemsValue)})");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Perfil", user.GetAvatarUrl())
            .WithThumbnailUrl(user.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Use os slash commands novos para evoluir seu personagem")
            .Build();
    }

private static string FormatSchooling(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Ensino Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Ensino Fundamental II",
        SchoolingLevel.EnsinoMedio => "Ensino Médio",
        SchoolingLevel.EnsinoSuperior => "Ensino Superior",
        _ => "Nenhuma"
    };

    private static string FormatLicenca(LicenseLevel level)
    {
        var info = LicenseProgression.Info(level);
        return $"{info.Name}";
    }

    private static string FormatFame(HallOfFame fame)
    {
        var sb = new StringBuilder($"**{fame.Title}**");
        if (!string.IsNullOrWhiteSpace(fame.Phase))
            sb.Append($" — {fame.Phase}");
if (!string.IsNullOrWhiteSpace(fame.Phrase))
            sb.Append($"\n   *“{fame.Phrase}”*");
        return sb.ToString();
    }
}