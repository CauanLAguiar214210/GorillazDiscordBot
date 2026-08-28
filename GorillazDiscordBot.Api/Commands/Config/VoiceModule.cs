using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Config;

public class VoiceModule : ModuleBase<SocketCommandContext>
{
    private readonly ISettingsRepository<GuildVoiceSettings> _voiceRepository;

    public VoiceModule(ISettingsRepository<GuildVoiceSettings> voiceRepository)
    {
        _voiceRepository = voiceRepository;
    }

    [Command("voice setup")]
    [Summary("Define o canal de voz criador. Uso: macaco voice setup #canal")]
    public async Task VoiceSetupAsync(IVoiceChannel? channel = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (channel == null)
        {
            await ReplyAsync("❌ Uso: `macaco voice setup #canal` (canal de voz)");
            return;
        }

        var settings = await _voiceRepository.GetAsync(Context.Guild.Id);
        settings.CreatorChannelId = channel.Id;
        settings.Enabled = true;
        await _voiceRepository.SaveAsync(settings);

        await ReplyAsync(
            $"✅ Canal criador definido para {channel.Name} e ativado!\n" +
            "Quando alguém entrar nele, o bot cria um canal de voz privado com o nome da pessoa.");
    }

    [Command("voice off")]
    [Summary("Desativa a criação automática de canais de voz")]
    public async Task VoiceOffAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _voiceRepository.GetAsync(Context.Guild.Id);
        settings.Enabled = false;
        await _voiceRepository.SaveAsync(settings);

        await ReplyAsync("✅ Criação automática de canais de voz desativada.");
    }

    [Command("voice config")]
    [Summary("Mostra a configuração atual de canais de voz")]
    public async Task VoiceConfigAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var settings = await _voiceRepository.GetAsync(Context.Guild.Id);

        var channelName = settings.CreatorChannelId.HasValue
            ? Context.Guild.GetVoiceChannel(settings.CreatorChannelId.Value)?.Name ?? "Canal não encontrado"
            : BotConstants.NotSet;

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Canais de Voz")
            .WithGoldTheme()
            .WithStatus("Status", settings.Enabled)
            .AddField("Canal criador", channelName, true)
            .WithDescription("Ao entrar no canal criador, o bot cria um canal de voz privado com o seu nome.")
            .WithStandardFooter("Use macaco voice setup #canal para alterar")
            .Build();

        await ReplyAsync(embed: embed);
    }
}
