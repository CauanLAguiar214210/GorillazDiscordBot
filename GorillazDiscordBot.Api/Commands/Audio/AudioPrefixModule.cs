using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Commands.Audio;

[RequireContext(ContextType.Guild)]
public class AudioPrefixModule : ModuleBase<SocketCommandContext>
{
    private const int LocalSoundsPageSize = 25;

    private readonly IAudioPlayerService _audioPlayer;
    private readonly IPersistentVoiceService _persistentVoice;
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<AudioUploadOptions> _uploadOptions;

    public AudioPrefixModule(
        IAudioPlayerService audioPlayer,
        IPersistentVoiceService persistentVoice,
        ISettingsRepository<Guild> guildRepository,
        IOptions<AudioUploadOptions> uploadOptions)
    {
        _audioPlayer = audioPlayer;
        _persistentVoice = persistentVoice;
        _guildRepository = guildRepository;
        _uploadOptions = uploadOptions;
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

    [Command("join")]
    [Summary("Entra e permanece no canal de voz")]
    public async Task JoinAsync(IVoiceChannel? canal = null)
    {
        var voiceChannel = canal ?? GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await ReplyAsync("🔇 Entre em um canal de voz ou informe um canal.");
            return;
        }

        var result = await _persistentVoice.JoinAsync(Context.Guild.Id, voiceChannel.Id);
        await ReplyAsync(result.Success
            ? $"🔊 Entrei em **{voiceChannel.Name}** e permanecerei aqui até `leave` ou o canal ficar vazio."
            : $"❌ {result.Error}");
    }

    [Command("leave")]
    [Summary("Sai do canal de voz")]
    public async Task LeaveAsync()
    {
        var result = await _persistentVoice.LeaveAsync(Context.Guild.Id);
        await ReplyAsync(result.WasPlaying
            ? "👋 Parei o áudio e saí do canal."
            : "👋 Saí do canal de voz.");
    }

    [Command("parar")]
    [Summary("Para o som atual")]
    public async Task PararAsync()
    {
        var result = await _audioPlayer.StopAsync(Context.Guild.Id);
        await ReplyAsync(result.WasPlaying
            ? "⏹️ Reprodução parada."
            : "ℹ️ Nada tocando no momento.");
    }

    [Command("sons")]
    [Summary("Lista os sons locais disponíveis no servidor de música")]
    public async Task SonsAsync()
    {
        var directory = LocalSoundCatalog.GetSoundsDirectory(AppContext.BaseDirectory);
        var sounds = LocalSoundCatalog.List(directory)
            .Concat(LocalSoundCatalog.List(_uploadOptions.Value.Path, AudioUploadService.RelativeFolder))
            .OrderBy(sound => sound.FileName)
            .ToList();

        if (sounds.Count == 0)
        {
            await ReplyAsync(
                "🎧 Nenhum som local encontrado.\n" +
                "Coloque arquivos de áudio (`.mp3`, `.ogg`, `.wav`, `.flac`) em " +
                "`GorillazDiscordBot.Api/Resources/Sounds/` e use `macaco tocar local:arquivo`.");
            return;
        }

        // O Discord aceita no máximo 25 fields por embed e 10 embeds por mensagem;
        // passar disso (76+ sons locais) faz o Build() lançar e o comando falhar.
        var pages = sounds.Chunk(LocalSoundsPageSize).ToList();
        for (var i = 0; i < pages.Count; i++)
        {
            var embed = new EmbedBuilder()
                .WithBlurpleTheme()
                .WithTitle("🎧 Sons locais disponíveis")
                .WithDescription("Use `macaco tocar local:nome-do-arquivo` — ex.: `local:alarme.mp3`")
                .WithStandardFooter($"{sounds.Count} som(es) carregado(s) no servidor de música · Parte {i + 1}/{pages.Count}");

            foreach (var sound in pages[i])
                embed.AddField(sound.FileName, $"`local:{sound.RelativePath}`", true);

            await ReplyAsync(embed: embed.Build());
        }
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

        var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.ScheduledSounds.Add(schedule);
            return true;
        });

        if (!saved)
        {
            await ReplyAsync("❌ Não consegui salvar o agendamento. Tente novamente.");
            return;
        }

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
        string? removeError = null;
        string? removedSource = null;
        var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            var schedule = guild.ScheduledSounds.FirstOrDefault(s =>
                s.Id.ToString().Equals(key, StringComparison.OrdinalIgnoreCase)
                || s.Id.ToString().StartsWith(key, StringComparison.OrdinalIgnoreCase));

            if (schedule == null)
            {
                removeError = "Agendamento não encontrado.";
                return false;
            }

            removedSource = schedule.AudioSource;
            guild.ScheduledSounds.Remove(schedule);
            return true;
        });

        if (removeError != null)
        {
            await ReplyAsync($"❌ {removeError}");
            return;
        }

        if (!saved)
        {
            await ReplyAsync("❌ Não consegui remover o agendamento. Tente novamente.");
            return;
        }

        await ReplyAsync($"🗑️ Som agendado removido: `{removedSource}`");
    }

    [Command("som-entrar")]
    [Summary("Define o som tocado quando alguém entra no canal de voz (admin). Use 'off' para desativar")]
    public async Task SomEntrarAsync([Remainder] string origem)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (IsOff(origem))
        {
            var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                guild.JoinVoiceSound = null;
                return true;
            });
            await ReplyAsync(saved
                ? "🔕 Som ao **entrar** desativado."
                : "❌ Não consegui salvar a alteração. Tente novamente.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var newValue = origem.Trim();
        var savedOn = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.JoinVoiceSound = newValue;
            return true;
        });
        await ReplyAsync(savedOn
            ? $"🔊 Som ao **entrar** definido: `{newValue}`."
            : "❌ Não consegui salvar a alteração. Tente novamente.");
    }

    [Command("som-sair")]
    [Summary("Define o som tocado quando alguém sai do canal de voz (admin). Use 'off' para desativar")]
    public async Task SomSairAsync([Remainder] string origem)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (IsOff(origem))
        {
            var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                guild.LeaveVoiceSound = null;
                return true;
            });
            await ReplyAsync(saved
                ? "🔕 Som ao **sair** desativado."
                : "❌ Não consegui salvar a alteração. Tente novamente.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var newValue = origem.Trim();
        var savedOn = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.LeaveVoiceSound = newValue;
            return true;
        });
        await ReplyAsync(savedOn
            ? $"🔊 Som ao **sair** definido: `{newValue}`."
            : "❌ Não consegui salvar a alteração. Tente novamente.");
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
