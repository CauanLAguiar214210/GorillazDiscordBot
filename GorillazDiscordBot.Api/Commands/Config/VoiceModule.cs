using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Config;

public class VoiceModule : ModuleBase<SocketCommandContext>
{
    private readonly ISettingsRepository<Guild> _guildRepository;

    public VoiceModule(ISettingsRepository<Guild> guildRepository)
    {
        _guildRepository = guildRepository;
    }

    [Command("voice setup")]
    [Summary("Adiciona/reativa um canal criador de voz. Uso: macaco voice setup #canal [limite] [nome-padrão]")]
    public async Task VoiceSetupAsync(IVoiceChannel? channel = null, int? limite = null, [Remainder] string? template = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco voice setup #canal [limite] [nome-padrão]` (canal de voz)");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var settings = guild.VoiceChannels.FirstOrDefault(v => v.CreatorChannelId == channel.Id);
        if (settings == null)
        {
            settings = new VoiceChannelSettings { CreatorChannelId = channel.Id };
            guild.VoiceChannels.Add(settings);
        }

        settings.Enabled = true;
        if (limite is > 0)
            settings.UserLimit = limite;
        if (!string.IsNullOrWhiteSpace(template))
            settings.NameTemplate = template;

        await _guildRepository.SaveAsync(guild);

        await ReplyAsync(
            $"✅ Canal criador `{channel.Name}` ativado!\n" +
            "Quando alguém entrar nele, o bot cria um canal de voz privado com o nome da pessoa.");
    }

    [Command("voice off")]
    [Summary("Desativa um canal criador de voz. Uso: macaco voice off #canal")]
    public async Task VoiceOffAsync(IVoiceChannel? channel = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco voice off #canal` (canal de voz)");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var settings = guild.VoiceChannels.FirstOrDefault(v => v.CreatorChannelId == channel.Id);

        if (settings == null)
        {
            await ReplyAsync($"❌ `{channel.Name}` não está configurado como canal criador.");
            return;
        }

        settings.Enabled = false;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"✅ Canal criador `{channel.Name}` desativado.");
    }

    [Command("voice remove")]
    [Summary("Remove um canal criador de voz. Uso: macaco voice remove #canal")]
    public async Task VoiceRemoveAsync(IVoiceChannel? channel = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco voice remove #canal` (canal de voz)");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var removed = guild.VoiceChannels.RemoveAll(v => v.CreatorChannelId == channel.Id) > 0;
        if (!removed)
        {
            await ReplyAsync($"❌ `{channel.Name}` não está configurado como canal criador.");
            return;
        }

        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"✅ Canal criador `{channel.Name}` removido.");
    }

    [Command("voice config")]
    [Summary("Mostra a configuração atual dos canais de voz")]
    public async Task VoiceConfigAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.VoiceChannels.Count == 0)
        {
            await ReplyAsync("Este servidor não tem canais criadores de voz configurados.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Canais de Voz")
            .WithGoldTheme()
            .WithDescription("Ao entrar num canal criador, o bot cria um canal de voz privado com o nome da pessoa.")
            .WithStandardFooter("Use macaco voice setup #canal para adicionar");

        foreach (var settings in guild.VoiceChannels)
        {
            var channel = Context.Guild.GetVoiceChannel(settings.CreatorChannelId);
            var name = settings.NameTemplate ?? VoiceChannelSettings.DefaultNameTemplate;
            var limit = settings.UserLimit ?? VoiceChannelSettings.DefaultUserLimit;

            embed = embed.AddField(
                channel != null ? channel.Name : $"Canal removido ({settings.CreatorChannelId})",
                $"Status: {(settings.Enabled ? "✅ ativo" : "⛔ desativado")}\n" +
                $"Nome padrão: `{name}`\n" +
                $"Limite: {limit}",
                true);
        }

        await ReplyAsync(embed: embed.Build());
    }
}