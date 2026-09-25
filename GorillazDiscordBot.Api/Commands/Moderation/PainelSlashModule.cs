using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("painel", "Configuracao de paineis de mensagens deste servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class PainelSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public PainelSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual dos paineis")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Paineis")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("criar", "Cria um novo painel")]
    public async Task CriarAsync(
        [Summary("nome", "Nome do painel")] string nome,
        [Summary("canal", "Canal onde o painel sera postado")] IChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Channels[nome] = canal.Id;
        await SaveAsync(settings);

        await RespondAsync($"Painel `/painel sessao {nome}` criado no canal {MentionUtils.MentionChannel(canal.Id)}.");
    }

    [SlashCommand("remover", "Remove um painel")]
    public async Task RemoverAsync(
        [Summary("nome", "Nome do painel")] string nome)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Channels.Remove(nome);
        await SaveAsync(settings);

        await RespondAsync($"Painel `/painel sessao {nome}` removido.");
    }

    private async Task<PanelSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Panel;
    }

    private async Task SaveAsync(PanelSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Panel = settings;
        await _guildRepository.SaveAsync(guild);
    }
}