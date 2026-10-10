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
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IOptions<AudioUploadOptions> _uploadOptions;

    public AudioSlashModule(
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

    [SlashCommand("add", "Adiciona um áudio não local à lista do servidor")]
    public async Task AddAsync(string origem, string nome)
    {
        await DeferAsync(ephemeral: true);

        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await FollowupAsync(BotConstants.PermissionDenied, ephemeral: true);
            return;
        }

        if (!FavoriteSoundMatcher.TryNormalizeAlias(nome, out var alias, out var aliasError))
        {
            await FollowupAsync($"❌ {aliasError}", ephemeral: true);
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}", ephemeral: true);
            return;
        }

        if (resolved.Kind == AudioOriginKind.Local)
        {
            await FollowupAsync("❌ Use este comando apenas para áudios não locais.", ephemeral: true);
            return;
        }

        // Para som instantâneo, ResolveAsync troca a origem pela URL do espelho,
        // que é volátil: o favorito precisa guardar o link do Myinstants.
        var source = resolved.Kind == AudioOriginKind.InstantButton
            ? origem.Trim()
            : resolved.Identifier!;

        string? error = null;
        var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            if (FavoriteSoundMatcher.FindBySource(guild.FavoriteSounds, source) != null)
            {
                error = "Já existe um áudio cadastrado com este link ou origem.";
                return false;
            }

            if (FavoriteSoundMatcher.FindByAlias(guild.FavoriteSounds, alias!) != null)
            {
                error = $"Já existe um áudio cadastrado com o nome **{alias}**.";
                return false;
            }

            if (guild.FavoriteSounds.Count >= FavoriteSoundMatcher.MaxFavorites)
            {
                error = $"Limite de {FavoriteSoundMatcher.MaxFavorites} áudios atingido.";
                return false;
            }

            guild.FavoriteSounds.Add(new FavoriteSoundSettings
            {
                AudioSource = source,
                Alias = alias
            });
            return true;
        });

        if (error != null)
        {
            await FollowupAsync($"❌ {error}", ephemeral: true);
            return;
        }

        if (!saved)
        {
            await FollowupAsync("❌ Não consegui salvar o áudio. Tente novamente.", ephemeral: true);
            return;
        }

        await FollowupAsync(
            $"✅ Áudio **{alias}** adicionado à lista. Use `/audio listar` para encontrá-lo.",
            ephemeral: true);
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
        if (string.IsNullOrWhiteSpace(selected))
        {
            await RespondAsync("❌ Áudio inválido.", ephemeral: true);
            return;
        }

        await DeferAsync();
        var filter = ParseAudioListFilter(filterValue);
        var item = await GetAudioListItemByKeyAsync(filter, selected);
        if (item is null)
        {
            await FollowupAsync("❌ Esse áudio não está mais disponível.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(message =>
        {
            message.Embed = BuildAudioDetailEmbed(item).Build();
            message.Components = BuildAudioDetailComponents(filter, page, item);
        });
    }

    [ComponentInteraction("audio-catalog:action:*:*:*:*", true)]
    public async Task AudioListActionAsync(string filterValue, int page, string key, string action)
    {
        var filter = ParseAudioListFilter(filterValue);
        if (action.Equals("back", StringComparison.OrdinalIgnoreCase))
        {
            await DeferAsync();
            await RenderAudioListAsync(filter, page);
            return;
        }

        var item = await GetAudioListItemByKeyAsync(filter, key);
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
            string? actionError = null;
            string? successMessage = null;
            var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                var favorite = FindFavorite(guild, item);

                if (action == "favorite")
                {
                    if (!item.IsLocal || favorite != null)
                    {
                        actionError = "Esse áudio já está nos favoritos ou não é um som local disponível.";
                        return false;
                    }

                    if (guild.FavoriteSounds.Count >= FavoriteSoundMatcher.MaxFavorites)
                    {
                        actionError = $"Limite de {FavoriteSoundMatcher.MaxFavorites} favoritos atingido.";
                        return false;
                    }

                    guild.FavoriteSounds.Add(new FavoriteSoundSettings { AudioSource = item.Source });
                    successMessage = "⭐ Áudio favoritado.";
                    return true;
                }

                if (favorite == null)
                {
                    actionError = "Favorito não encontrado.";
                    return false;
                }

                guild.FavoriteSounds.Remove(favorite);
                successMessage = action == "remove" ? "🗑️ Áudio não local removido." : "💔 Áudio desfavoritado.";
                return true;
            });

            if (actionError != null)
            {
                await FollowupAsync($"❌ {actionError}", ephemeral: true);
                return;
            }

            if (!saved)
            {
                await FollowupAsync("❌ Não consegui salvar a alteração. Tente novamente.", ephemeral: true);
                return;
            }

            await FollowupAsync(successMessage!, ephemeral: true);

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
            await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                var favorite = FindFavorite(guild, item);
                if (favorite == null)
                    return false;

                favorite.TimesPlayed++;
                return true;
            });
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

        var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.ScheduledSounds.Add(schedule);
            return true;
        });

        if (!saved)
        {
            await FollowupAsync("❌ Não consegui salvar o agendamento. Tente novamente.");
            return;
        }

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
            await FollowupAsync($"❌ {removeError}");
            return;
        }

        if (!saved)
        {
            await FollowupAsync("❌ Não consegui remover o agendamento. Tente novamente.");
            return;
        }

        await FollowupAsync($"🗑️ Som agendado removido: `{removedSource}`");
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
            var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                guild.JoinVoiceSound = null;
                return true;
            });
            await FollowupAsync(saved
                ? "🔕 Som ao **entrar** desativado."
                : "❌ Não consegui salvar a alteração. Tente novamente.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var newValue = origem.Trim();
        var savedOn = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.JoinVoiceSound = newValue;
            return true;
        });
        await FollowupAsync(savedOn
            ? $"🔊 Som ao **entrar** definido: `{newValue}`."
            : "❌ Não consegui salvar a alteração. Tente novamente.");
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
            var saved = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
            {
                guild.LeaveVoiceSound = null;
                return true;
            });
            await FollowupAsync(saved
                ? "🔕 Som ao **sair** desativado."
                : "❌ Não consegui salvar a alteração. Tente novamente.");
            return;
        }

        var resolved = await _audioPlayer.ResolveAsync(origem);
        if (!resolved.IsValid)
        {
            await FollowupAsync($"❌ {resolved.Error}");
            return;
        }

        var newValue = origem.Trim();
        var savedOn = await _guildRepository.MutateAsync(Context.Guild.Id, guild =>
        {
            guild.LeaveVoiceSound = newValue;
            return true;
        });
        await FollowupAsync(savedOn
            ? $"🔊 Som ao **sair** definido: `{newValue}`."
            : "❌ Não consegui salvar a alteração. Tente novamente.");
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
            .WithDescription("Selecione um áudio para ver as ações disponíveis. Use o filtro ⭐ Favoritos para ver só os seus favoritos.")
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

    private async Task<AudioListItem?> GetAudioListItemByKeyAsync(AudioListFilter filter, string key)
    {
        var guild = await _guildRepository.GetAsync(Context.Guild.Id);
        return BuildAudioListItems(guild, filter)
            .FirstOrDefault(item => string.Equals(CatalogKey(item.Source), key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Chave estável do item nos custom IDs: derivada da origem, não da posição.
    /// Índices de página mudam entre o render e o clique (outro admin favoritou,
    /// arquivo novo, filtro trocado) e apontariam para o áudio errado.
    /// </summary>
    internal static string CatalogKey(string source)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(source.ToLowerInvariant()));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
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
                .WithOptions(pageItems.Select(item =>
                    new SelectMenuOptionBuilder(
                        FavoriteSoundMatcher.Truncate(item.DisplayName, 90),
                        CatalogKey(item.Source),
                        item.IsLocal ? "Áudio local" : "Favorito não local")).ToList()));
        }

        if (pageCount > 1)
        {
            components.AddRow(new ActionRowBuilder()
                .WithButton("Anterior", $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page - 1}:-:back", ButtonStyle.Secondary, disabled: page == 0)
                .WithButton("Próxima", $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page + 1}:-:back", ButtonStyle.Secondary, disabled: page == pageCount - 1));
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
        AudioListItem item)
    {
        var prefix = $"audio-catalog:action:{filter.ToString().ToLowerInvariant()}:{page}:{CatalogKey(item.Source)}";
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
