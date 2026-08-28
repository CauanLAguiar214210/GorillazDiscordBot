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

    private readonly ISettingsRepository<GuildPrefixSettings> _prefixRepository;
    private readonly IOptions<BotOptions> _botOptions;

    public PrefixModule(
        ISettingsRepository<GuildPrefixSettings> prefixRepository,
        IOptions<BotOptions> botOptions)
    {
        _prefixRepository = prefixRepository;
        _botOptions = botOptions;
    }

    [Command("prefix")]
    [Summary("Mostra o prefixo de comandos deste servidor")]
    public async Task PrefixAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _prefixRepository.GetAsync(Context.Guild.Id);
        var current = GetCurrentPrefix(settings);

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

        var settings = await _prefixRepository.GetAsync(Context.Guild.Id);
        settings.Prefix = prefixo + " ";
        await _prefixRepository.SaveAsync(settings);

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

        await _prefixRepository.ResetAsync(Context.Guild.Id);

        var defaultPrefix = _botOptions.Value.CommandPrefix;
        await ReplyAsync($"✅ Prefixo resetado! Voltou ao padrão global: `{defaultPrefix}`");
    }

    internal string GetCurrentPrefix(GuildPrefixSettings settings)
        => !string.IsNullOrWhiteSpace(settings.Prefix)
            ? settings.Prefix
            : _botOptions.Value.CommandPrefix;
}
