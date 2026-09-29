using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Audio;

[Group("audio", "Comandos de áudio no canal de voz")]
[RequireContext(ContextType.Guild)]
public class AudioSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ISettingsRepository<Guild> _guildRepository;

    public AudioSlashModule(
        IAudioPlayerService audioPlayer,
        ISettingsRepository<Guild> guildRepository)
    {
        _audioPlayer = audioPlayer;
        _guildRepository = guildRepository;
    }

    [SlashCommand("teste", "Toca o som de teste oficial (óleo de macaco) no seu canal de voz")]
    public async Task TesteAsync()
    {
        await DeferAsync();

        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await FollowupAsync("🔇 Entre em um canal de voz para usar este comando.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync($"local:{LocalSoundCatalog.TestSoundFileName}");
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);
        await FollowupAsync(result.Success
            ? $"🧪 Som de teste tocando em **{voiceChannel.Name}** — horário oficial do óleo de macaco! 🐒"
            : $"❌ {result.Error}");
    }

    [SlashCommand("tocar", "Toca um som no canal de voz (YouTube, busca, Myinstants, URL de áudio ou local:arquivo)")]
    public async Task TocarAsync(string origem)
    {
        await DeferAsync();

        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await FollowupAsync("🔇 Entre em um canal de voz para usar este comando.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        if (resolved.RequiresElevatedPermission && !CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);
        await FollowupAsync(result.Success
            ? $"🎵 Tocando no **{voiceChannel.Name}**... saio quando acabar!"
            : $"❌ {result.Error}");
    }

    [SlashCommand("parar", "Para o som atual e sai do canal de voz")]
    public async Task PararAsync()
    {
        await DeferAsync();

        var result = await _audioPlayer.StopAsync(Context.Guild.Id);
        await FollowupAsync(result.WasPlaying
            ? "⏹️ Reprodução parada — saí do canal."
            : "ℹ️ Nada tocando no momento.");
    }

    [SlashCommand("sons", "Lista os sons locais disponíveis no servidor de música")]
    public async Task SonsAsync()
    {
        await DeferAsync();

        var directory = LocalSoundCatalog.GetSoundsDirectory(AppContext.BaseDirectory);
        var sounds = LocalSoundCatalog.List(directory);

        if (sounds.Count == 0)
        {
            await FollowupAsync(
                "🎧 Nenhum som local encontrado.\n" +
                "Coloque arquivos de áudio em `GorillazDiscordBot.Api/Resources/Sounds/` e use `/audio tocar local:arquivo`.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🎧 Sons locais disponíveis")
            .WithDescription("Use `/audio tocar local:nome-do-arquivo` — ex.: `local:alarme.mp3`")
            .WithStandardFooter($"{sounds.Count} som(es) carregado(s) no servidor de música");

        foreach (var sound in sounds)
            embed.AddField(sound.FileName, $"`local:{sound.RelativePath}`", true);

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("agendar", "Agenda um som para tocar no canal de voz (horário de Brasília)")]
    public async Task AgendarAsync(string origem, string hora, DayOfWeek? dia = null, IVoiceChannel? canal = null)
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        if (!ScheduleEvaluator.TryParseSaoPaulo(hora, out var utcTime))
        {
            await FollowupAsync($"❌ Horário inválido: `{hora}`. Use o formato `HH:MM` (ex.: `14:30`).");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var targetChannel = canal ?? GetRequesterVoiceChannel();
        if (targetChannel == null)
        {
            await FollowupAsync("⚠️ Informe um canal de voz ou entre em um antes de agendar.");
            return;
        }

        var schedule = new ScheduledSoundSettings
        {
            AudioSource = origem.Trim(),
            Day = dia,
            Time = utcTime,
            VoiceChannelId = targetChannel.Id
        };

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        guild.ScheduledSounds.Add(schedule);
        await _guildRepository.SaveAsync(guild);

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🎵 Som agendado!")
            .AddField("Origem", $"`{schedule.AudioSource}`", true)
            .AddField("Horário", $"{ScheduleEvaluator.FormatSaoPauloTime(schedule.Time)} (Brasília)", true)
            .AddField("Dia", ScheduleEvaluator.FormatDayName(schedule.Day), true)
            .AddField("Canal", targetChannel.Name, true)
            .AddField("Id", $"`{schedule.Id}`", false)
            .WithStandardFooter("Use /audio desagendar <id> para remover")
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("agendamentos", "Lista os sons agendados do servidor")]
    public async Task AgendamentosAsync()
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.ScheduledSounds.Count == 0)
        {
            await FollowupAsync("📭 Nenhum som agendado. Use `/audio agendar` para criar um.");
            return;
        }

        var lines = guild.ScheduledSounds
            .OrderBy(s => s.Time)
            .Select(s => FormatScheduleLine(s, Context.Guild));

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle($"🎵 Sons agendados ({guild.ScheduledSounds.Count})")
            .WithDescription(string.Join("\n", lines))
            .WithStandardFooter("Use /audio desagendar <id> para remover")
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("desagendar", "Remove um som agendado")]
    public async Task DesagendarAsync(string id)
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        var key = id.Trim();
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var schedule = guild.ScheduledSounds.FirstOrDefault(s =>
            s.Id.ToString().Equals(key, StringComparison.OrdinalIgnoreCase)
            || s.Id.ToString().StartsWith(key, StringComparison.OrdinalIgnoreCase));

        if (schedule == null)
        {
            await FollowupAsync("❌ Agendamento não encontrado.");
            return;
        }

        guild.ScheduledSounds.Remove(schedule);
        await _guildRepository.SaveAsync(guild);

        await FollowupAsync($"🗑️ Som agendado removido: `{schedule.AudioSource}`");
    }

    [SlashCommand("som-entrada", "Define o som ao entrar no canal de voz (off desativa)")]
    public async Task SomEntradaAsync(string origem)
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        if (IsOff(origem))
        {
            var guild = await _guildRepository.GetAsync(Context.Guild.Id);
            guild.JoinVoiceSound = null;
            await _guildRepository.SaveAsync(guild);
            await FollowupAsync("🔕 Som ao **entrar** desativado.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var guild2 = await _guildRepository.GetAsync(Context.Guild.Id);
        guild2.JoinVoiceSound = origem.Trim();
        await _guildRepository.SaveAsync(guild2);
        await FollowupAsync($"🔊 Som ao **entrar** definido: `{guild2.JoinVoiceSound}`.");
    }

    [SlashCommand("som-saida", "Define o som ao sair do canal de voz (off desativa)")]
    public async Task SomSaidaAsync(string origem)
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        if (IsOff(origem))
        {
            var guild = await _guildRepository.GetAsync(Context.Guild.Id);
            guild.LeaveVoiceSound = null;
            await _guildRepository.SaveAsync(guild);
            await FollowupAsync("🔕 Som ao **sair** desativado.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var guild2 = await _guildRepository.GetAsync(Context.Guild.Id);
        guild2.LeaveVoiceSound = origem.Trim();
        await _guildRepository.SaveAsync(guild2);
        await FollowupAsync($"🔊 Som ao **sair** definido: `{guild2.LeaveVoiceSound}`.");
    }

    [SlashCommand("som-status", "Mostra os sons configurados para entrada/saída de canal de voz")]
    public async Task SomStatusAsync()
    {
        await DeferAsync();

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied);
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🔊 Sons de canal de voz")
            .AddField("Entrar", guild.JoinVoiceSound != null ? $"`{guild.JoinVoiceSound}`" : BotConstants.NotSet, true)
            .AddField("Sair", guild.LeaveVoiceSound != null ? $"`{guild.LeaveVoiceSound}`" : BotConstants.NotSet, true)
            .WithStandardFooter("Use /audio som-entrada <origem|off> · /audio som-saida <origem|off>")
            .Build();

        await FollowupAsync(embed: embed);
    }

    private static bool IsOff(string origem)
        => origem.Trim().Equals("off", StringComparison.OrdinalIgnoreCase)
           || origem.Trim().Equals("nenhum", StringComparison.OrdinalIgnoreCase);

    private string FormatScheduleLine(ScheduledSoundSettings schedule, SocketGuild guild)
    {
        var channel = guild.GetVoiceChannel(schedule.VoiceChannelId)?.Name ?? $"canal removido ({schedule.VoiceChannelId})";
        var status = schedule.Enabled ? "✅" : "⛔";
        return $"`{schedule.Id}` — **{schedule.AudioSource}** — " +
               $"{ScheduleEvaluator.FormatDayName(schedule.Day)} {ScheduleEvaluator.FormatSaoPauloTime(schedule.Time)} " +
               $"— {channel} — 🔁 {schedule.TimesPlayed}x — {status}";
    }

    private IVoiceChannel? GetRequesterVoiceChannel()
        => Context.User is IGuildUser guildUser ? guildUser.VoiceChannel : null;
}