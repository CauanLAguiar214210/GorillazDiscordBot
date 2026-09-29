using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Audio;

[RequireContext(ContextType.Guild)]
public class AudioPrefixModule : ModuleBase<SocketCommandContext>
{
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ISettingsRepository<Guild> _guildRepository;

    public AudioPrefixModule(
        IAudioPlayerService audioPlayer,
        ISettingsRepository<Guild> guildRepository)
    {
        _audioPlayer = audioPlayer;
        _guildRepository = guildRepository;
    }

    [Command("teste")]
    [Summary("Toca o som de teste oficial (óleo de macaco) no seu canal de voz")]
    public async Task TesteAsync()
    {
        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await ReplyAsync("🔇 Entre em um canal de voz para usar este comando.");
            return;
        }

        var directory = LocalSoundCatalog.GetSoundsDirectory(AppContext.BaseDirectory);
        var localSounds = LocalSoundCatalog.List(directory);
        if (!localSounds.Any(s => s.FileName == LocalSoundCatalog.TestSoundFileName))
        {
            await ReplyAsync(
                $"❌ Som de teste `{LocalSoundCatalog.TestSoundFileName}` não encontrado no servidor de música.\n" +
                "Coloque-o em `GorillazDiscordBot.Api/Resources/Sounds/` e rebuilda a imagem do Lavalink " +
                "(`docker compose build lavalink`).");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync($"local:{LocalSoundCatalog.TestSoundFileName}");
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);

        await ReplyAsync(result.Success
            ? $"🧪 Som de teste tocando em **{voiceChannel.Name}** — horário oficial do óleo de macaco! 🐒"
            : $"❌ {result.Error}");
    }

    [Command("tocar")]
    [Summary("Toca um som no canal de voz (YouTube, busca, Myinstants, URL de áudio ou `local:arquivo`)")]
    public async Task TocarAsync([Remainder] string origem)
    {
        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await ReplyAsync("🔇 Entre em um canal de voz para usar este comando.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        if (resolved.RequiresElevatedPermission && !await CommandGuards.GuardPermissionAsync(Context))
            return;

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);
        await ReplyAsync(result.Success
            ? $"🎵 Tocando no **{voiceChannel.Name}**... saio quando acabar!"
            : $"❌ {result.Error}");
    }

    [Command("parar")]
    [Summary("Para o som atual e sai do canal de voz")]
    public async Task PararAsync()
    {
        var result = await _audioPlayer.StopAsync(Context.Guild.Id);
        await ReplyAsync(result.WasPlaying
            ? "⏹️ Reprodução parada — saí do canal."
            : "ℹ️ Nada tocando no momento.");
    }

    [Command("sons")]
    [Summary("Lista os sons locais disponíveis no servidor de música")]
    public async Task SonsAsync()
    {
        var directory = LocalSoundCatalog.GetSoundsDirectory(AppContext.BaseDirectory);
        var sounds = LocalSoundCatalog.List(directory);

        if (sounds.Count == 0)
        {
            await ReplyAsync(
                "🎧 Nenhum som local encontrado.\n" +
                "Coloque arquivos de áudio (`.mp3`, `.ogg`, `.wav`, `.flac`) em " +
                "`GorillazDiscordBot.Api/Resources/Sounds/` e use `macaco tocar local:arquivo`.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🎧 Sons locais disponíveis")
            .WithDescription("Use `macaco tocar local:nome-do-arquivo` — ex.: `local:alarme.mp3`")
            .WithStandardFooter($"{sounds.Count} som(es) carregado(s) no servidor de música");

        foreach (var sound in sounds)
            embed.AddField(sound.FileName, $"`local:{sound.RelativePath}`", true);

        await ReplyAsync(embed: embed.Build());
    }

    [Command("agendar")]
    [Summary("Agenda um som para tocar no canal de voz (horário de Brasília)")]
    public async Task AgendarAsync(string origem, string hora, DayOfWeek? dia = null, IVoiceChannel? canal = null)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (!ScheduleEvaluator.TryParseSaoPaulo(hora, out var utcTime))
        {
            await ReplyAsync($"❌ Horário inválido: `{hora}`. Use o formato `HH:MM` (ex.: `14:30`).");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var targetChannel = canal ?? GetRequesterVoiceChannel();
        if (targetChannel == null)
        {
            await ReplyAsync("⚠️ Informe um canal de voz ou entre em um antes de agendar.");
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
            .AddField("Canal", $"{targetChannel.Name}", true)
            .AddField("Id", $"`{schedule.Id}`", false)
            .WithStandardFooter("Use macaco desagendar <id> para remover")
            .Build();

        await ReplyAsync(embed: embed);
    }

    [Command("agendamentos")]
    [Summary("Lista os sons agendados do servidor")]
    public async Task AgendamentosAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.ScheduledSounds.Count == 0)
        {
            await ReplyAsync("📭 Nenhum som agendado. Use `macaco agendar` para criar um.");
            return;
        }

        var lines = guild.ScheduledSounds
            .OrderBy(s => s.Time)
            .Select(s => FormatScheduleLine(s, Context.Guild));

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle($"🎵 Sons agendados ({guild.ScheduledSounds.Count})")
            .WithDescription(string.Join("\n", lines))
            .WithStandardFooter("Use macaco desagendar <id> para remover")
            .Build();

        await ReplyAsync(embed: embed);
    }

    [Command("desagendar")]
    [Summary("Remove um som agendado")]
    public async Task DesagendarAsync([Remainder] string id)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var key = id.Trim();
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var schedule = guild.ScheduledSounds.FirstOrDefault(s =>
            s.Id.ToString().Equals(key, StringComparison.OrdinalIgnoreCase)
            || s.Id.ToString().StartsWith(key, StringComparison.OrdinalIgnoreCase));

        if (schedule == null)
        {
            await ReplyAsync("❌ Agendamento não encontrado.");
            return;
        }

        guild.ScheduledSounds.Remove(schedule);
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"🗑️ Som agendado removido: `{schedule.AudioSource}`");
    }

    [Command("som-entrar")]
    [Summary("Define o som tocado quando alguém entra no canal de voz (admin). Use 'off' para desativar")]
    public async Task SomEntrarAsync([Remainder] string origem)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (IsOff(origem))
        {
            var guild = await _guildRepository.GetAsync(Context.Guild.Id);
            guild.JoinVoiceSound = null;
            await _guildRepository.SaveAsync(guild);
            await ReplyAsync("🔕 Som ao **entrar** desativado.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var guild2 = await _guildRepository.GetAsync(Context.Guild.Id);
        guild2.JoinVoiceSound = origem.Trim();
        await _guildRepository.SaveAsync(guild2);
        await ReplyAsync($"🔊 Som ao **entrar** definido: `{guild2.JoinVoiceSound}`.");
    }

    [Command("som-sair")]
    [Summary("Define o som tocado quando alguém sai do canal de voz (admin). Use 'off' para desativar")]
    public async Task SomSairAsync([Remainder] string origem)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (IsOff(origem))
        {
            var guild = await _guildRepository.GetAsync(Context.Guild.Id);
            guild.LeaveVoiceSound = null;
            await _guildRepository.SaveAsync(guild);
            await ReplyAsync("🔕 Som ao **sair** desativado.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var guild2 = await _guildRepository.GetAsync(Context.Guild.Id);
        guild2.LeaveVoiceSound = origem.Trim();
        await _guildRepository.SaveAsync(guild2);
        await ReplyAsync($"🔊 Som ao **sair** definido: `{guild2.LeaveVoiceSound}`.");
    }

    [Command("som-status")]
    [Summary("Mostra os sons configurados para entrada/saída de canal de voz (admin)")]
    public async Task SomStatusAsync()
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🔊 Sons de canal de voz")
            .AddField("Entrar", guild.JoinVoiceSound != null ? $"`{guild.JoinVoiceSound}`" : BotConstants.NotSet, true)
            .AddField("Sair", guild.LeaveVoiceSound != null ? $"`{guild.LeaveVoiceSound}`" : BotConstants.NotSet, true)
            .WithStandardFooter("Use: macaco som-entrar <origem|off> · macaco som-sair <origem|off>")
            .Build();

        await ReplyAsync(embed: embed);
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