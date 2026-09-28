using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("cooldown", "Configuracao de intervalos entre usos de comandos")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class CooldownSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public CooldownSlashModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [SlashCommand("status", "Mostra a configuracao atual de cooldowns")]
    public async Task StatusAsync()
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        var perCommand = settings.CommandSeconds
            .OrderBy(kv => kv.Key)
            .Select(kv => $"- `{kv.Key}`: {kv.Value}s");

        var embed = new EmbedBuilder()
            .WithTitle("⏳ Cooldowns")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Padrão", $"{settings.DefaultSeconds}s", true)
            .WithDescription(settings.CommandSeconds.Count == 0
                ? "Nenhum comando com cooldown personalizado."
                : $"**Por comando ({settings.CommandSeconds.Count}):**\n{string.Join("\n", perCommand)}")
            .WithStandardFooter("Use /cooldown definir <comando> <segundos>")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("ativar", "Ativa ou desativa os cooldowns")]
    public async Task AtivarAsync(
        [Summary("ativo", "true ativa, false desativa")] bool ativo)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.Enabled = ativo;
        await SaveAsync(settings);

        await RespondAsync(ativo
            ? "⏳ Cooldowns **ativados** neste servidor."
            : "⏳ Cooldowns **desativados** neste servidor.");
    }

    [SlashCommand("padrao", "Define o intervalo padrão para comandos sem cooldown personalizado")]
    public async Task PadraoAsync(
        [Summary("segundos", "Intervalo em segundos (0 desativa o padrão)")] int segundos)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.DefaultSeconds = Math.Clamp(segundos, 0, 3600);
        await SaveAsync(settings);

        await RespondAsync($"⏳ Cooldown padrão definido para **{settings.DefaultSeconds}s**.");
    }

    [SlashCommand("definir", "Define o intervalo entre usos de um comando")]
    public async Task DefinirAsync(
        [Summary("comando", "Nome do comando")] string comando,
        [Summary("segundos", "Intervalo em segundos (0 remove)")] int segundos)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        if (segundos <= 0)
            settings.CommandSeconds.Remove(comando);
        else
            settings.CommandSeconds[comando] = Math.Clamp(segundos, 1, 3600);
        await SaveAsync(settings);

        await RespondAsync(segundos <= 0
            ? $"⏳ Cooldown de `{comando}` removido."
            : $"⏳ Cooldown de `{comando}` definido para **{settings.CommandSeconds[comando]}s**.");
    }

    [SlashCommand("remover", "Remove o cooldown de um comando")]
    public async Task RemoverAsync(
        [Summary("comando", "Nome do comando")] string comando)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        var settings = await GetSettingsAsync();
        settings.CommandSeconds.Remove(comando);
        await SaveAsync(settings);

        await RespondAsync($"⏳ Cooldown de `{comando}` removido.");
    }

    private async Task<CooldownSettings> GetSettingsAsync()
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return guild.Cooldown;
    }

    private async Task SaveAsync(CooldownSettings settings)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Cooldown = settings;
        await _guildRepository.SaveAsync(guild);
    }
}