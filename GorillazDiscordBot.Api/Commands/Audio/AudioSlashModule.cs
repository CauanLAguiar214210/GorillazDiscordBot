using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Options;

namespace GorillazDiscordBot.Commands.Audio;

[Group("audio", "Comandos de áudio no canal de voz")]
[RequireContext(ContextType.Guild)]
public class AudioSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const int AudioListPageSize = 25;

    private readonly IAudioPlayerService _audioPlayer;
    private readonly IPersistentVoiceService _persistentVoice;
    private readonly IAudioUploadService _uploads;
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<AudioUploadOptions> _uploadOptions;

    public AudioSlashModule(
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

    [SlashCommand("join", "Entra e permanece no canal de voz")]
    public async Task JoinAsync(IVoiceChannel? canal = null)
    {
        await DeferAsync();

        var voiceChannel = canal ?? GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await FollowupAsync("🔇 Entre em um canal de voz ou informe um canal.");
            return;
        }

        var result = await _persistentVoice.JoinAsync(Context.Guild.Id, voiceChannel.Id);
        await FollowupAsync(result.Success
            ? $"🔊 Entrei em **{voiceChannel.Name}** e permanecerei aqui até `leave` ou o canal ficar vazio."
            : $"❌ {result.Error}");
    }

    [SlashCommand("leave", "Sai do canal de voz")]
    public async Task LeaveAsync()
    {
        await DeferAsync();
        var result = await _persistentVoice.LeaveAsync(Context.Guild.Id);
        await FollowupAsync(result.WasPlaying
            ? "👋 Parei o áudio e saí do canal."
            : "👋 Saí do canal de voz.");
    }

    [SlashCommand("parar", "Para o som atual")]
    public async Task PararAsync()
    {
        await DeferAsync();

        var result = await _audioPlayer.StopAsync(Context.Guild.Id);
        await FollowupAsync(result.WasPlaying
            ? "⏹️ Reprodução parada."
            : "ℹ️ Nada tocando no momento.");
    }

    [SlashCommand("listar", "Lista e gerencia sons locais e favoritos de áudio")]
    public async Task ListarAsync()
    {
        await DeferAsync();
        await RenderAudioListAsync(AudioListFilter.All, 0);
    }

    [ComponentInteraction("audio-catalog:filter", true)]
    public async Task AudioListFilterAsync()
    {
        await DeferAsync();
        var component = (SocketMessageComponent)Context.Interaction;
        var filter = ParseAudioListFilter(component.Data.Values.FirstOrDefault());
        await RenderAudioListAsync(filter, 0);
    }

    [ComponentInteraction("audio-catalog:select:*:*", true)]
    public async Task AudioListSelectAsync(string filterValue, int page)
    {
        var component = (SocketMessageComponent)Context.Interaction;
        var selected = component.Data.Values.FirstOrDefault();
        if (!int.TryParse(selected, out var index))
        {
            await RespondAsync("❌ Áudio inválido.", ephemeral: true);
            return;
        }

        await DeferAsync();
        var filter = ParseAudioListFilter(filterValue);
        var item = await GetAudioListItemAsync(filter, page, index);
        if (item is null)
        {
            await FollowupAsync("❌ Esse áudio não está mais disponível.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(message =>
        {
            message.Embed = BuildAudioDetailEmbed(item).Build();
            message.Components = BuildAudioDetailComponents(filter, page, index, item);
        });
    }

    [ComponentInteraction("audio-catalog:action:*:*:*:*", true)]
    public async Task AudioListActionAsync(string filterValue, int page, int index, string action)
    {
        var filter = ParseAudioListFilter(filterValue);
        if (action.Equals("back", StringComparison.OrdinalIgnoreCase))
        {
            await DeferAsync();
            await RenderAudioListAsync(filter, page);
            return;
        }

        var item = await GetAudioListItemAsync(filter, page, index);
        if (item is null)
        {
            await RespondAsync("❌ Esse áudio não está mais disponível.", ephemeral: true);
            return;
        }

        if (action is "favorite" or "unfavorite" or "remove")
        {
            if (!CommandGuards.HasManageGuildPermission(Context))
            {
                await RespondAsync(BotConstants.PermissionDenied, ephemeral: true);
                return;
            }

            await DeferAsync(true);
            var guild = await _guildRepository.GetAsync(Context.Guild.Id);
            var favorite = FindFavorite(guild, item);

            if (action == "favorite")
            {
                if (!item.IsLocal || favorite != null)
                {
                    await FollowupAsync("ℹ️ Esse áudio já está nos favoritos ou não é um som local disponível.", ephemeral: true);
                    return;
                }

                if (guild.FavoriteSounds.Count >= FavoriteSoundMatcher.MaxFavorites)
                {
                    await FollowupAsync($"❌ Limite de {FavoriteSoundMatcher.MaxFavorites} favoritos atingido.", ephemeral: true);
                    return;
                }

                guild.FavoriteSounds.Add(new FavoriteSoundSettings { AudioSource = item.Source });
                await _guildRepository.SaveAsync(guild);
                await FollowupAsync("⭐ Áudio favoritado.", ephemeral: true);
            }
            else
            {
                if (favorite == null)
                {
                    await FollowupAsync("❌ Favorito não encontrado.", ephemeral: true);
                    return;
                }

                guild.FavoriteSounds.Remove(favorite);
                await _guildRepository.SaveAsync(guild);
                await FollowupAsync(action == "remove" ? "🗑️ Áudio não local removido." : "💔 Áudio desfavoritado.", ephemeral: true);
            }

            await RenderAudioListAsync(filter, page);
            return;
        }

        if (action != "play")
        {
            await RespondAsync("❌ Ação inválida.", ephemeral: true);
            return;
        }

        await DeferAsync(true);
        var voiceChannel = GetRequesterVoiceChannel();
        if (voiceChannel == null)
        {
            await FollowupAsync("🔇 Entre em um canal de voz para reproduzir o áudio.", ephemeral: true);
            return;
        }

        var playGuild = await _guildRepository.GetAsync(Context.Guild.Id);
        var playFavorite = FindFavorite(playGuild, item);
        var resolved = await _audioPlayer.ResolveAsync(item.Source);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ Não consegui resolver o áudio: {resolved.Error}", ephemeral: true);
            return;
        }

        if (resolved.RequiresElevatedPermission && !CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied, ephemeral: true);
            return;
        }

        var result = await _audioPlayer.PlayAsync(Context.Guild.Id, voiceChannel.Id, resolved.Identifier!);
        if (!result.Success)
        {
            await FollowupAsync($"❌ {result.Error}", ephemeral: true);
            return;
        }

        if (playFavorite != null)
        {
            playFavorite.TimesPlayed++;
            await _guildRepository.SaveAsync(playGuild);
        }

        await FollowupAsync($"▶️ Tocando **{item.DisplayName}** no **{voiceChannel.Name}**.");
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

    private async Task RenderAudioListAsync(AudioListFilter filter, int requestedPage)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        var items = BuildAudioListItems(guild, filter);

        if (items.Count == 0)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(message =>
            {
                message.Embed = new EmbedBuilder()
                    .WithBlurpleTheme()
                    .WithTitle("🎧 Lista de áudios")
                    .WithDescription("Nenhum áudio encontrado com esse filtro.")
                    .Build();
                message.Components = BuildAudioListComponents(filter, 0, Array.Empty<AudioListItem>(), 1);
            });
            return;
        }

        var pageCount = (int)Math.Ceiling(items.Count / (double)AudioListPageSize);
        var page = Math.Clamp(requestedPage, 0, pageCount - 1);
        var pageItems = items.Skip(page * AudioListPageSize).Take(AudioListPageSize).ToList();

        var embed = new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle("🎧 Lista de áudios")
            .WithDescription("Selecione um áudio para ver as ações disponíveis. O filtro ⭐ Favoritos substitui o antigo `/audio favs`.")
            .WithStandardFooter($"Filtro: {FormatAudioListFilter(filter)} · Página {page + 1}/{pageCount} · {items.Count} áudio(s)");

        foreach (var item in pageItems)
        {
            var kind = item.IsLocal ? "📁 Local" : "🔗 Não local";
            var alias = item.Favorite?.Alias;
            var title = string.IsNullOrWhiteSpace(alias) ? item.DisplayName : $"{alias} · {item.DisplayName}";
            embed.AddField($"{kind} · {title}", $"`{item.Source}`", false);
        }

        await Context.Interaction.ModifyOriginalResponseAsync(message =>
        {
            message.Embed = embed.Build();
            message.Components = BuildAudioListComponents(filter, page, pageItems, pageCount);
        });
    }

    private async Task<AudioListItem?> GetAudioListItemAsync(AudioListFilter filter, int page, int index)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return BuildAudioListItems(guild, filter)
            .Skip(page * AudioListPageSize)
            .ElementAtOrDefault(index);
    }

    private List<AudioListItem> BuildAudioListItems(Guild guild, AudioListFilter filter)
    {
        var directory = LocalSoundCatalog.GetSoundsDirectory(AppContext.BaseDirectory);
        var localSounds = LocalSoundCatalog.List(directory)
            .Concat(LocalSoundCatalog.List(_uploadOptions.Value.Path, AudioUploadService.RelativeFolder))
            .OrderBy(sound => sound.FileName)
            .ToList();
        var items = new List<AudioListItem>();
        var localSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sound in localSounds)
        {
            var source = LocalSource(sound.RelativePath);
            localSources.Add(source);
            var favorite = guild.FavoriteSounds.FirstOrDefault(f =>
                f.AudioSource.Equals(source, StringComparison.OrdinalIgnoreCase));
            items.Add(new AudioListItem(sound.FileName, source, true, favorite?.Id, favorite));
        }

        items.AddRange(guild.FavoriteSounds
            .Where(f => !localSources.Contains(f.AudioSource))
            .Select(f => new AudioListItem(
                FavoriteSoundMatcher.DisplayName(f), f.AudioSource, false, f.Id, f)));

        return filter switch
        {
            AudioListFilter.Favorites => items.Where(i => i.FavoriteId.HasValue).ToList(),
            AudioListFilter.Local => items.Where(i => i.IsLocal).ToList(),
            AudioListFilter.NonLocal => items.Where(i => !i.IsLocal).ToList(),
            _ => items
        };
    }

    private static MessageComponent BuildAudioListComponents(
        AudioListFilter filter,
        int page,
        IReadOnlyList<AudioListItem> pageItems,
        int pageCount)
    {
        var components = new ComponentBuilder().WithSelectMenu(new SelectMenuBuilder()
            .WithCustomId("audio-catalog:filter")
            .WithPlaceholder("Filtrar áudios…")
            .WithOptions(Enum.GetValues<AudioListFilter>().Select(value =>
                new SelectMenuOptionBuilder(FormatAudioListFilter(value), value.ToString().ToLowerInvariant())
                {
                    IsDefault = value == filter
                }).ToList()));

        if (pageItems.Count > 0)
        {
            components.WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"audio-catalog:select:{filter.ToString().ToLowerInvariant()}:{page}")
                .WithPlaceholder("Selecione um áudio…")
                .WithOptions(pageItems.Select((item, index) =>
                    new SelectMenuOptionBuilder(
                        FavoriteSoundMatcher.Truncate(item.DisplayName, 90),
                        index.ToString(),
                        item.IsLocal ? "Áudio local" : "Favorito não local")).ToList()));
        }

        if (pageCount > 1)
        {
            components.AddRow(new ActionRowBuilder()
                .WithButton("Anterior", $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page - 1}:0:back", ButtonStyle.Secondary, disabled: page == 0)
                .WithButton("Próxima", $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page + 1}:0:back", ButtonStyle.Secondary, disabled: page == pageCount - 1));
        }

        return components.Build();
    }

    private static EmbedBuilder BuildAudioDetailEmbed(AudioListItem item)
        => new EmbedBuilder()
            .WithBlurpleTheme()
            .WithTitle($"🎧 {item.DisplayName}")
            .AddField("Tipo", item.IsLocal ? "Local" : "Não local", true)
            .AddField("Favorito", item.FavoriteId.HasValue ? "Sim" : "Não", true)
            .AddField("Origem", $"`{FavoriteSoundMatcher.Truncate(item.Source, 900)}`", false)
            .WithStandardFooter("Use Voltar para retornar à lista");

    private static MessageComponent BuildAudioDetailComponents(
        AudioListFilter filter,
        int page,
        int index,
        AudioListItem item)
    {
        var prefix = $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page}:{index}";
        var row = new ActionRowBuilder()
            .WithButton("Tocar", $"{prefix}:play", ButtonStyle.Primary)
            .WithButton("Voltar", $"{prefix}:back", ButtonStyle.Secondary);

        if (item.FavoriteId.HasValue)
        {
            row.WithButton("Desfavoritar", $"{prefix}:unfavorite", ButtonStyle.Danger);
            if (!item.IsLocal)
                row.WithButton("Remover", $"{prefix}:remove", ButtonStyle.Danger);
        }
        else if (item.IsLocal)
        {
            row.WithButton("Favoritar", $"{prefix}:favorite", ButtonStyle.Success);
        }

        return new ComponentBuilder().AddRow(row).Build();
    }

    private static FavoriteSoundSettings? FindFavorite(Guild guild, AudioListItem item)
        => guild.FavoriteSounds.FirstOrDefault(f =>
            f.AudioSource.Equals(item.Source, StringComparison.OrdinalIgnoreCase));

    private static AudioListFilter ParseAudioListFilter(string? value)
        => Enum.TryParse<AudioListFilter>(value, true, out var filter) ? filter : AudioListFilter.All;

    private static string FormatAudioListFilter(AudioListFilter filter)
        => filter switch
        {
            AudioListFilter.Favorites => "⭐ Favoritos",
            AudioListFilter.Local => "📁 Locais",
            AudioListFilter.NonLocal => "🔗 Não locais",
            _ => "🎧 Todos"
        };

    private static string LocalSource(string relativePath)
        => $"local:sounds/{relativePath.Replace('\\', '/')}";

    private sealed record AudioListItem(
        string DisplayName,
        string Source,
        bool IsLocal,
        Guid? FavoriteId,
        FavoriteSoundSettings? Favorite)
    {
    }

    private enum AudioListFilter
    {
        All,
        Favorites,
        Local,
        NonLocal
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
