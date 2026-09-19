using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Release;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Release;

[Group("release", "Novidades e changelog do bot")]
[RequireContext(ContextType.Guild)]
public class ReleaseSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const int MaxListedReleases = 10;
    private const int MaxAllListedReleases = 100;

    private readonly IReleaseNoteRepository _releaseRepository;
    private readonly ReleaseAnnouncementService _announcer;

    public ReleaseSlashModule(
        IReleaseNoteRepository releaseRepository,
        ReleaseAnnouncementService announcer)
    {
        _releaseRepository = releaseRepository;
        _announcer = announcer;
    }

    [SlashCommand("listar", "Mostra as atualizações do bot")]
    public async Task ListarAsync(
        [Summary("todas", "Lista o changelog completo (padrão: as 10 mais recentes)")] bool todas = false)
    {
        var releases = await _releaseRepository.GetRecentAsync(todas ? MaxAllListedReleases : MaxListedReleases);
        await RespondAsync(embed: ReleaseEmbedBuilder.BuildList(releases, todas ? releases.Count : MaxListedReleases));
    }

    [SlashCommand("anunciar", "Reenvia o anúncio de uma versão no canal de novidades")]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task AnunciarAsync(
        [Summary("versao", "Versão específica (padrão: a mais recente)")] string? versao = null)
    {
        if (!await CommandGuards.GuardAdminInteractionAsync(Context))
            return;

        await DeferAsync(ephemeral: true);

        ReleaseNote? release;
        try
        {
            release = string.IsNullOrWhiteSpace(versao)
                ? await _releaseRepository.GetLatestAsync()
                : await _releaseRepository.GetByVersionAsync(versao.Trim());
        }
        catch
        {
            await FollowupAsync("❌ Falha ao buscar a release no banco de dados.", ephemeral: true);
            return;
        }

        if (release == null)
        {
            var message = string.IsNullOrWhiteSpace(versao)
                ? "❌ Nenhuma release encontrada."
                : $"❌ Versão `{versao}` não encontrada.";
            await FollowupAsync(message, ephemeral: true);
            return;
        }

        var result = await _announcer.AnnounceToGuildAsync(Context.Guild, release);
        if (!result.Sent)
        {
            await FollowupAsync($"❌ {result.Reason}", ephemeral: true);
            return;
        }

        await FollowupAsync($"✅ Release `{release.Version}` anunciada no canal de novidades!", ephemeral: true);
    }
}
