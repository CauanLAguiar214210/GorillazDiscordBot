using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

[Group("som")]
public class SoundModule : ModuleBase<SocketCommandContext>
{
    private static readonly Regex TriggerRegex = new(@"^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxSoundsPerGuild = 50;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".ogg", ".oga", ".wav", ".m4a", ".mp4", ".webm", ".opus", ".flac"
    };

    private const string HelpText =
        "🔊 **Sons**\n" +
        "`macaco som add <nome> <url>` — cadastra um som (mp3, ogg, wav, m4a, mp4, webm, opus, flac; máx. 8 MB)\n" +
        "　↳ Links de páginas do MyInstants também funcionam (ex.: `myinstants.com/pt/instant/...`)\n" +
        "`macaco som list` — lista os sons do servidor\n" +
        "`macaco som remove <nome>` — remove um som\n" +
        "`macaco som play <nome>` — toca um som agora\n" +
        "`macaco skip` — pula o som atual\n" +
        "Com o bot em um canal de voz (`macaco join`), basta digitar o nome do som no chat para tocar.";

    private readonly ISoundInteractionRepository _soundRepository;
    private readonly IAudioFileStorage _audioStorage;
    private readonly IVoicePresenceService _voicePresence;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IHttpClientFactory _httpClientFactory;

    public SoundModule(
        ISoundInteractionRepository soundRepository,
        IAudioFileStorage audioStorage,
        IVoicePresenceService voicePresence,
        IAudioPlaybackService playbackService,
        IHttpClientFactory httpClientFactory)
    {
        _soundRepository = soundRepository;
        _audioStorage = audioStorage;
        _voicePresence = voicePresence;
        _playbackService = playbackService;
        _httpClientFactory = httpClientFactory;
    }

    [Command]
    [Summary("Mostra como usar os comandos de som")]
    public Task HelpAsync() => ReplyAsync(HelpText);

    [Command("add")]
    [Summary("Cadastra um som de áudio por URL. Uso: macaco som add <nome> <url>")]
    public async Task AddAsync(string nome = "", string url = "")
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var trigger = nome.Trim().ToLowerInvariant();
        if (!TriggerRegex.IsMatch(trigger))
        {
            await ReplyAsync("❌ Nome inválido. Use apenas letras minúsculas, números, `-` ou `_` (máx. 32 caracteres).");
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await ReplyAsync("❌ URL inválida. Use: `macaco som add <nome> <url>`");
            return;
        }

        if (MyInstantsResolver.IsPageUrl(uri))
        {
            try
            {
                uri = await ResolveMyInstantsPageAsync(uri);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"❌ Falha ao resolver o som do MyInstants: {ex.Message}");
                return;
            }
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            var formato = string.IsNullOrEmpty(extension) ? "(nenhum)" : extension;
            await ReplyAsync(
                $"❌ Formato `{formato}` não suportado. Use o link direto do arquivo de áudio. " +
                $"Formatos aceitos: {string.Join(", ", AllowedExtensions)}.");
            return;
        }

        if (await _soundRepository.GetAsync(Context.Guild.Id, trigger) != null)
        {
            await ReplyAsync($"❌ Já existe um som com o nome `{trigger}` neste servidor.");
            return;
        }

        var sounds = await _soundRepository.GetAllAsync(Context.Guild.Id);
        if (sounds.Count >= MaxSoundsPerGuild)
        {
            await ReplyAsync($"❌ Limite de {MaxSoundsPerGuild} sons por servidor atingido. Remova algum som antes de adicionar outro.");
            return;
        }

        byte[] audioBytes;
        try
        {
            audioBytes = await DownloadAudioAsync(uri);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"❌ Falha ao baixar o áudio: {ex.Message}");
            return;
        }

        string gridFsId;
        await using (var stream = new MemoryStream(audioBytes))
        {
            gridFsId = await _audioStorage.SaveAsync(stream, fileName);
        }

        var sound = new GuildSoundInteraction
        {
            GuildId = Context.Guild.Id,
            Trigger = trigger,
            FileName = fileName,
            GridFsId = gridFsId,
            FileLengthBytes = audioBytes.Length,
            ContentType = extension.ToLowerInvariant(),
            AddedBy = Context.User.Id,
            CreatedAt = DateTime.UtcNow
        };

        if (!await _soundRepository.AddAsync(sound))
        {
            await _audioStorage.DeleteAsync(gridFsId);
            await ReplyAsync($"❌ Já existe um som com o nome `{trigger}` neste servidor.");
            return;
        }

        await ReplyAsync(
            $"✅ Som `{trigger}` adicionado ({FormatBytes(audioBytes.Length)})!\n" +
            "Com o bot em um canal de voz (`macaco join`), digite `" + trigger + "` no chat para tocar.");
    }

    [Command("list")]
    [Summary("Lista os sons cadastrados no servidor")]
    public async Task ListAsync()
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var sounds = await _soundRepository.GetAllAsync(Context.Guild.Id);
        if (sounds.Count == 0)
        {
            await ReplyAsync("📭 Nenhum som cadastrado ainda. Use `macaco som add <nome> <url>` para adicionar.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var sound in sounds.OrderBy(s => s.Trigger))
            sb.AppendLine($"`{sound.Trigger}` → {sound.FileName} ({FormatBytes(sound.FileLengthBytes)})");

        var embed = new EmbedBuilder()
            .WithTitle("🔊 Sons do servidor")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter($"Total: {sounds.Count}/{MaxSoundsPerGuild} · Use macaco som add <nome> <url>")
            .Build();

        await ReplyAsync(embed: embed);
    }

    [Command("remove")]
    [Summary("Remove um som do servidor. Uso: macaco som remove <nome>")]
    public async Task RemoveAsync(string nome = "")
    {
        if (!await CommandGuards.GuardPermissionAsync(Context))
            return;
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var trigger = nome.Trim().ToLowerInvariant();
        var sound = await _soundRepository.GetAsync(Context.Guild.Id, trigger);
        if (sound == null)
        {
            await ReplyAsync($"❌ O som `{trigger}` não existe neste servidor.");
            return;
        }

        await _soundRepository.RemoveAsync(Context.Guild.Id, trigger);
        await _audioStorage.DeleteAsync(sound.GridFsId);

        await ReplyAsync($"✅ Som `{trigger}` removido.");
    }

    [Command("play")]
    [Summary("Toca um som cadastrado no canal de voz. Uso: macaco som play <nome>")]
    public async Task PlayAsync([Remainder] string nome = "")
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var trigger = nome.Trim().ToLowerInvariant();
        var sound = await _soundRepository.GetAsync(Context.Guild.Id, trigger);
        if (sound == null)
        {
            await ReplyAsync($"❌ O som `{trigger}` não existe neste servidor. Use `macaco som list` para ver os disponíveis.");
            return;
        }

        if (!_voicePresence.IsConnected(Context.Guild.Id))
        {
            await ReplyAsync("❌ Não estou em nenhum canal de voz. Use `macaco join` primeiro.");
            return;
        }

        _playbackService.Enqueue(Context.Guild.Id, sound);
        await ReplyAsync($"▶️ `{trigger}` na fila (posição {_playbackService.GetQueueLength(Context.Guild.Id)}).");
    }

    private async Task<Uri> ResolveMyInstantsPageAsync(Uri pageUri)
    {
        var client = _httpClientFactory.CreateClient("SoundDownload");
        using var response = await client.GetAsync(pageUri);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        return MyInstantsResolver.ExtractAudioUrl(html, pageUri)
            ?? throw new InvalidOperationException(
                "não encontrei o áudio nessa página. Tente o link direto do MP3 (botão \"Baixar MP3\" no MyInstants).");
    }

    private async Task<byte[]> DownloadAudioAsync(Uri uri)
    {
        var client = _httpClientFactory.CreateClient("SoundDownload");
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var declaredLength = response.Content.Headers.ContentLength;
        if (declaredLength.HasValue && declaredLength.Value > MaxFileBytes)
            throw new InvalidOperationException(
                $"o arquivo tem {FormatBytes(declaredLength.Value)} e o limite é {FormatBytes(MaxFileBytes)}.");

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream();

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await contentStream.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > MaxFileBytes)
                throw new InvalidOperationException($"o arquivo excede o limite de {FormatBytes(MaxFileBytes)}.");

            await output.WriteAsync(buffer.AsMemory(0, read));
        }

        if (output.Length == 0)
            throw new InvalidOperationException("o arquivo baixado está vazio.");

        return output.ToArray();
    }

    private static string FormatBytes(long bytes)
        => bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.#} MB",
            >= 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes} B"
        };
}

public class SkipModule : ModuleBase<SocketCommandContext>
{
    private readonly IAudioPlaybackService _playbackService;

    public SkipModule(IAudioPlaybackService playbackService)
    {
        _playbackService = playbackService;
    }

    [Command("skip")]
    [Summary("Pula o som que está tocando agora")]
    public async Task SkipAsync()
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return;

        var skipped = _playbackService.SkipCurrent(Context.Guild.Id);
        await ReplyAsync(skipped ? "⏭️ Som pulado." : "❌ Nada está tocando agora.");
    }
}
