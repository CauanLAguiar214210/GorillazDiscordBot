using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class VoiceModule : ModuleBase<SocketCommandContext>
{
    private readonly ISettingsRepository<GuildVoiceSettings> _voiceRepository;
    private readonly IVoicePresenceService _voicePresence;

    public VoiceModule(ISettingsRepository<GuildVoiceSettings> voiceRepository, IVoicePresenceService voicePresence)
    {
        _voiceRepository = voiceRepository;
        _voicePresence = voicePresence;
    }

    [Command("join")]
    [Summary("Entra no seu canal de voz e permanece lá. Uso: macaco join")]
    public async Task JoinAsync(IVoiceChannel? channel = null)
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var targetChannel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
        if (targetChannel == null)
        {
            await ReplyAsync("❌ Entre em um canal de voz e use `macaco join`, ou use `macaco join <#canal>`.");
            return;
        }

        var (success, error) = await _voicePresence.JoinAsync(Context.Guild, targetChannel);
        if (!success)
        {
            await ReplyAsync($"❌ Não consegui entrar em **{targetChannel.Name}**: {error}");
            return;
        }

        await ReplyAsync(
            $"✅ Entrei em **{targetChannel.Name}** e vou ficar por aqui!\n" +
            "Digite o nome de um som cadastrado (`macaco som list`) para eu tocar.");
    }

    [Command("leave")]
    [Summary("Sai do canal de voz")]
    public async Task LeaveAsync()
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        await _voicePresence.LeaveAsync(Context.Guild);
        await ReplyAsync("👋 Saí do canal de voz.");
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
