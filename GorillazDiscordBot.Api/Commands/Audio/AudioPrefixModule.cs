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
    private readonly IAudioPlayerService _audioPlayer;
    private readonly IPersistentVoiceService _persistentVoice;
    private readonly IAudioUploadService _uploads;
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<AudioUploadOptions> _uploadOptions;

    public AudioPrefixModule(
        IAudioPlayerService audioPlayer,
        IPersistentVoiceService persistentVoice,
        IAudioUploadService uploads,
        ISettingsRepository<Guild> guildRepository,
        IOptions<AudioUploadOptions> uploadOptions)
    {
        _audioPlayer = audioPlayer;
        _persistentVoice = persistentVoice;
        _uploads = uploads;
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

    [Summary("Opção `macaco favs favoritar <origem> [= apelido>` (admin)")]
    public async Task FavoritarAsync([Remainder] string entrada)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        if (!FavoriteSoundMatcher.TryParseInput(entrada, out var origem, out var alias, out var inputError))
        {
            await ReplyAsync($"❌ {inputError}");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ {resolved.Error}");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.FavoriteSounds.Count >= FavoriteSoundMatcher.MaxFavorites)
        {
            await ReplyAsync($"❌ Limite de {FavoriteSoundMatcher.MaxFavorites} favoritos atingido. Remova algum com `macaco favs desfavoritar`.");
            return;
        }

        if (alias != null && guild.FavoriteSounds.Any(f => f.Alias!.Equals(alias, StringComparison.OrdinalIgnoreCase)))
        {
            await ReplyAsync($"❌ Já existe um favorito com o apelido **{alias}**.");
            return;
        }

        // A origem é guardada como o usuário digitou, normalizada pelo resolver
        // (ex.: `local:uploads/<id>.mp3`), para sobreviver a restart.
        var favorite = new FavoriteSoundSettings
        {
            AudioSource = resolved.Identifier!,
            Alias = alias
        };

        guild.FavoriteSounds.Add(favorite);
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync(embed: BuildFavoriteAddedEmbed(favorite, guild.FavoriteSounds.Count).Build());
    }

    [Summary("Opção `macaco favs favoritar-anexo [= apelido]` (admin)")]
    public async Task FavoritarAnexoAsync([Remainder] string apelido)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == null)
        {
            await ReplyAsync("📎 Anexe um arquivo de áudio ao comando. Ex.: `macaco favs favoritar-anexo = risada`");
            return;
        }

        string? alias = null;
        if (!string.IsNullOrWhiteSpace(apelido))
        {
            if (!FavoriteSoundMatcher.TryNormalizeAlias(apelido, out var parsedAlias, out var aliasError))
            {
                await ReplyAsync($"❌ {aliasError}");
                return;
            }

            alias = parsedAlias;
        }

        await ReplyAsync("⏳ Enviando para o servidor de música...");

        var upload = await _uploads.UploadAsync(new Uri(attachment.Url), attachment.Filename);
        if (!upload.Success)
        {
            await ReplyAsync($"❌ {upload.Error}");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.FavoriteSounds.Count >= FavoriteSoundMatcher.MaxFavorites)
        {
            await ReplyAsync($"❌ Limite de {FavoriteSoundMatcher.MaxFavorites} favoritos atingido.");
            return;
        }

        var favorite = new FavoriteSoundSettings
        {
            AudioSource = upload.Origin!,
            Alias = alias
        };

        guild.FavoriteSounds.Add(favorite);
        await _guildRepository.SaveAsync(guild);

        var embed = BuildFavoriteAddedEmbed(favorite, guild.FavoriteSounds.Count)
            .AddField("Arquivo", attachment.Filename, true);

        if (upload.Duration is { } duration)
            embed.AddField("Duração", AudioUploadService.FormatDuration(duration), true);

        await ReplyAsync(embed: embed.Build());
    }

    [Summary("Lista os favoritos de áudio da guilda")]
    public async Task FavsAsync([Remainder] string? acao = null)
    {
        var tokens = (acao ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            var argumento = tokens.Length == 2 ? tokens[1] : string.Empty;
            switch (tokens[0].ToLowerInvariant())
            {
                case "favoritar":
                    await FavoritarAsync(argumento);
                    return;
                case "favoritar-anexo":
                    await FavoritarAnexoAsync(argumento);
                    return;
                case "desfavoritar":
                    await DesfavoritarAsync(argumento);
                    return;
                case "tocarfav":
                case "tocar-fav":
                    await TocarFavAsync(argumento);
                    return;
            }

            await ReplyAsync("❌ Opção inválida. Use `macaco favs`, `macaco favs favoritar`, `macaco favs desfavoritar` ou `macaco favs tocarfav`.");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (guild.FavoriteSounds.Count == 0)
        {
            await ReplyAsync("📭 Nenhum favorito ainda. Use `macaco favs favoritar <origem>` ou anexe um áudio em `macaco favs favoritar-anexo`.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle($"⭐ Favoritos de áudio ({guild.FavoriteSounds.Count})")
            .WithDescription("Use `macaco tocar fav <número|apelido>` ou `macaco tocar fav aleatorio`.")
            .WithStandardFooter("Use macaco favs favoritar <origem> [= apelido] para adicionar");

        for (var i = 0; i < guild.FavoriteSounds.Count; i++)
        {
            var favorite = guild.FavoriteSounds[i];
            var label = string.IsNullOrWhiteSpace(favorite.Alias) ? FavoriteSoundMatcher.Truncate(favorite.AudioSource, 60) : favorite.Alias!;
            var plays = favorite.TimesPlayed > 0 ? $" — 🔁 {favorite.TimesPlayed}x" : string.Empty;
            embed.AddField($"{i + 1}. {label}", $"`{favorite.AudioSource}`{plays}", false);
        }

        await ReplyAsync(embed: embed.Build());
    }

    [Summary("Opção `macaco favs desfavoritar <número|apelido|id>` (admin)")]
    public async Task DesfavoritarAsync([Remainder] string chave)
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        if (!FavoriteSoundMatcher.TryFind(guild.FavoriteSounds, chave, out var favorite, out var findError))
        {
            await ReplyAsync($"❌ {findError}");
            return;
        }

        guild.FavoriteSounds.Remove(favorite!);
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"🗑️ Favorito removido: **{FavoriteSoundMatcher.DisplayName(favorite!)}**");
    }

    [Summary("Opção `macaco favs tocarfav <número|apelido|aleatorio>`")]
    public async Task TocarFavAsync([Remainder] string chave)
    {
        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await ReplyAsync("🔇 Entre em um canal de voz para usar este comando.");
            return;
        }

        var guild = await _guildRepository.GetAsync(Context.Guild.Id);

        FavoriteSoundSettings? favorite;
        if (FavoriteSoundMatcher.IsRandomRequest(chave))
        {
            favorite = FavoriteSoundMatcher.PickRandom(guild.FavoriteSounds);
            if (favorite == null)
            {
                await ReplyAsync("📭 Nenhum favorito ainda. Use `macaco favs favoritar <origem>`.");
                return;
            }
        }
        else if (!FavoriteSoundMatcher.TryFind(guild.FavoriteSounds, chave, out var found, out var findError))
        {
            await ReplyAsync($"❌ {findError}");
            return;
        }
        else
        {
            favorite = found;
        }

        // A permissão é reavaliada a cada execução: um favorito de URL direta
        // criado por um admin não pode ser tocado por qualquer membro.
        var resolved = await _audioPlayer.ResolveAsync(favorite!.AudioSource);
        if (!resolved.IsValid)
        {
            await ReplyAsync($"❌ Não consegui resolver o favorito: {resolved.Error}");
            return;
        }

        if (resolved.RequiresElevatedPermission && !await CommandGuards.GuardPermissionAsync(Context))
            return;

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);
        if (!result.Success)
        {
            await ReplyAsync($"❌ {result.Error}");
            return;
        }

        favorite.TimesPlayed++;
        await _guildRepository.SaveAsync(guild);

        await ReplyAsync($"⭐ Tocando **{FavoriteSoundMatcher.DisplayName(favorite)}** no **{voiceChannel.Name}**... saio quando acabar!");
    }

    private static EmbedBuilder BuildFavoriteAddedEmbed(FavoriteSoundSettings favorite, int total)
        => new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("⭐ Favorito salvo!")
            .AddField("Origem", $"`{FavoriteSoundMatcher.Truncate(favorite.AudioSource, 200)}`", false)
            .AddField("Apelido", string.IsNullOrWhiteSpace(favorite.Alias) ? BotConstants.NotSet : $"**{favorite.Alias}**", true)
            .AddField("Total de favoritos", $"{total}/{FavoriteSoundMatcher.MaxFavorites}", true)
            .WithStandardFooter("Use macaco favs tocarfav " +
                                (string.IsNullOrWhiteSpace(favorite.Alias) ? "<número>" : favorite.Alias!) +
                                " para tocar");

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
