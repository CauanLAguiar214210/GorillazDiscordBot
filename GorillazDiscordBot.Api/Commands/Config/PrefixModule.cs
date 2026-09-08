using Discord;
using Discord.Commands;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Api.Commands.Config;

public class PrefixModule : ModuleBase<SocketCommandContext>
{
    private const int MaxPrefixLength = 10;

    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<BotOptions> _botOptions;

    public PrefixModule(
        ISettingsRepository<Guild> guildRepository,
        IOptions<BotOptions> botOptions)
    {
        _guildRepository = guildRepository;
        _botOptions = botOptions;
    }

    [Command("prefix")]
    [Summary("Mostra o prefixo de comandos deste servidor")]
    public async Task PrefixAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var current = GetCurrentPrefix(guild);

        await ReplyAsync($"⚙️ Prefixo de comandos deste servidor: `{current}`");
    }

    [Command("prefix set")]
    [Summary("Define um novo prefixo de comandos para este servidor")]
    public async Task PrefixSetAsync([Remainder] string novoPrefix)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var prefixo = novoPrefix.Trim();
        if (prefixo.Length == 0)
        {
            await ReplyAsync("❌ Uso: `prefix set <novo-prefixo>`");
            return;
        }

        if (prefixo.Length > MaxPrefixLength)
        {
            await ReplyAsync($"❌ O prefixo deve ter no máximo **{MaxPrefixLength}** caracteres.");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Prefix.Prefix = prefixo + " ";
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync(
            $"✅ Prefixo definido para `{prefixo}`!\n" +
            $"A partir de agora use `{prefixo}ajuda` para ver os comandos.\n" +
            $"Lembrete: mencionar o bot também funciona sempre (ex.: `@{Context.Client.CurrentUser.Username} prefix`)");
    }

    [Command("prefix reset")]
    [Summary("Volta o prefixo deste servidor ao padrão global")]
    public async Task PrefixResetAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.Prefix.Prefix = null;
        await _guildRepository.SaveAsync(guild);

        var defaultPrefix = _botOptions.Value.CommandPrefix;
        await ReplyAsync($"✅ Prefixo resetado! Voltou ao padrão global: `{defaultPrefix}`");
    }

    internal string GetCurrentPrefix(Guild guild)
        => !string.IsNullOrWhiteSpace(guild.Prefix.Prefix)
            ? guild.Prefix.Prefix
            : _botOptions.Value.CommandPrefix;
}
