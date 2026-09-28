using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("boas-vindas", "Configuracao de mensagens de boas vindas deste servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class BoasVindasSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public BoasVindasSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual das boas vindas")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var embed = new EmbedBuilder()
            .WithTitle("Boas-Vindas")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("criar", "Define a mensagem de boas vindas")]
    public async Task CriarAsync(
        [Summary("nome", "Nome da mensagem")] string nome,
        [Summary("canal", "Canal onde a mensagem sera postada")] IChannel canal)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.WelcomeChannelId = canal.Id;
        settings.WelcomeEnabled = true;
        await SaveAsync(settings);

        await RespondAsync($"Mensagem de boas-vindas /{nome} definida no canal {MentionUtils.MentionChannel(canal.Id)}.");
    }

    [SlashCommand("remover", "Remove a mensagem de boas vindas")]
    public async Task RemoverAsync(
        [Summary("nome", "Nome da mensagem")] string nome)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.WelcomeEnabled = false;
        settings.WelcomeChannelId = null;
        await SaveAsync(settings);

        await RespondAsync($"Mensagem de boas-vindas /{nome} removida.");
    }

    private async Task<WelcomeSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Welcome;
    }

    private async Task SaveAsync(WelcomeSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Welcome = settings;
        await _guildRepository.SaveAsync(guild);
    }
}