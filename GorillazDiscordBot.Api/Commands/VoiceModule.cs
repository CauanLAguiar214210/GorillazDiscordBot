using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;

namespace GorillazDiscordBot.Commands;

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
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

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
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = await _voiceRepository.GetAsync(Context.Guild.Id);
        settings.Enabled = false;
        await _voiceRepository.SaveAsync(settings);

        await ReplyAsync("✅ Criação automática de canais de voz desativada.");
    }

    [Command("voice config")]
    [Summary("Mostra a configuração atual de canais de voz")]
    public async Task VoiceConfigAsync()
    {
        if (!HasPermission())
        {
            await ReplyAsync("❌ Você precisa da permissão **Gerenciar Servidor** para usar este comando.");
            return;
        }

        var settings = await _voiceRepository.GetAsync(Context.Guild.Id);

        var status = settings.Enabled ? "🟢 Ativado" : "🔴 Desativado";
        var channelName = settings.CreatorChannelId.HasValue
            ? Context.Guild.GetVoiceChannel(settings.CreatorChannelId.Value)?.Name ?? "Canal não encontrado"
            : "Não definido";

        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Configuração de Canais de Voz")
            .WithColor(Color.Gold)
            .AddField("Status", status, true)
            .AddField("Canal criador", channelName, true)
            .WithDescription("Ao entrar no canal criador, o bot cria um canal de voz privado com o seu nome.")
            .WithFooter("Use macaco voice setup #canal para alterar")
            .Build();

        await ReplyAsync(embed: embed);
    }

    private bool HasPermission()
        => Context.Guild != null &&
           Context.User is IGuildUser guildUser &&
           (guildUser.GuildPermissions.ManageGuild || guildUser.GuildPermissions.Administrator);
}
