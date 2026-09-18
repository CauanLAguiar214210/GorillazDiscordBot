using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Moderation;

[Group("moderacao", "Moderação e limpeza do servidor")]
[RequireContext(ContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageMessages)]
public class ModerationSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IGuildMemberRepository _memberRepository;

    public ModerationSlashModule(IGuildMemberRepository memberRepository)
    {
        _memberRepository = memberRepository;
    }

    [SlashCommand("limpar", "Apaga até N mensagens do canal (opcional: só de um usuário)")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task LimparAsync(
        [Summary("quantidade", "Quantas mensagens apagar (padrão 50, máximo 2000)")] int quantidade = 50,
        [Summary("usuário", "Filtra as mensagens de um usuário")] IUser? usuario = null)
    {
        if (Context.Channel is not ITextChannel textChannel)
        {
            await RespondAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.ManageMessages))
            return;

        await DeferAsync();

        quantidade = Math.Clamp(quantidade, 1, MessagePurge.MaxPurge);

        var messages = (await textChannel.GetMessagesAsync(quantidade + 1, mode: CacheMode.AllowDownload).FlattenAsync()).ToList();
        var plan = MessagePurge.PlanDelete(messages, usuario?.Id, DateTime.UtcNow);

        var deleted = 0;
        var failures = 0;
        foreach (var chunk in plan.BulkChunks)
        {
            try
            {
                if (chunk.Count == 1)
                    await chunk[0].DeleteAsync();
                else
                    await textChannel.DeleteMessagesAsync(chunk);
                deleted += chunk.Count;
            }
            catch
            {
                failures += chunk.Count;
            }
        }

        foreach (var message in plan.Individual)
        {
            try
            {
                await message.DeleteAsync();
                deleted++;
            }
            catch
            {
                failures++;
            }
        }

        var embed = new EmbedBuilder()
            .WithTitle("🧹 Chat limpo")
            .WithGoldTheme()
            .WithDescription(deleted == 0
                ? "Nenhuma mensagem para remover."
                : $"✅ Removidas **{deleted}** mensagens{(usuario != null ? $" de {usuario.Mention}" : "")}."
                  + (failures > 0 ? $"\n⚠️ {failures} não puderam ser apagadas." : ""))
            .WithStandardFooter("Mensagens fixadas foram preservadas")
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("limpar-tudo", "Recria o canal apagando todo o histórico")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task LimparTudoAsync(
        [Summary("canal", "Canal a recriar (padrão: atual)")] ITextChannel? canal = null)
    {
        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await RespondAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        if (!CommandGuards.BotHasPermission(Context, GuildPermission.ManageChannels)
            || !CommandGuards.BotHasPermission(Context, GuildPermission.ManageMessages))
        {
            await RespondAsync("⚠️ O bot precisa das permissões **Gerenciar Canais** e **Gerenciar Mensagens** para executar esta ação.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        var clone = await ChannelClone.CloneAsync(target);
        await target.DeleteAsync();

        await ((IMessageChannel)clone).SendMessageAsync($"🧹 Canal recriado por **{Context.User.GetDisplayName()}** — histórico apagado.");
        await ModifyOriginalResponseAsync(m => m.Content = "✅ Canal totalmente limpo!");
    }

    [SlashCommand("expulsar", "Expulsa um membro do servidor")]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task ExpulsarAsync(
        [Summary("usuário", "Membro a expulsar")] IUser usuario,
        [Summary("motivo", "Motivo da expulsão")] string? motivo = null)
    {
        if (Context.Guild.GetUser(usuario.Id) is not IGuildUser guildUser)
        {
            await RespondAsync("❌ Esse usuário não está no servidor.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.KickMembers))
            return;

        try
        {
            await guildUser.KickAsync(motivo ?? "Sem motivo informado");
            await RespondAsync($"👢 **{usuario.GetDisplayName()}** foi expulso{(motivo != null ? $" — {motivo}" : ".")}");
        }
        catch (Exception ex)
        {
            await RespondAsync($"❌ Não foi possível expulsar: {ex.Message}");
        }
    }

    [SlashCommand("banir", "Bane um membro do servidor")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task BanirAsync(
        [Summary("usuário", "Membro a banir")] IUser usuario,
        [Summary("motivo", "Motivo do banimento")] string? motivo = null,
        [Summary("dias", "Dias de mensagens a apagar (0-7)")] int dias = 0)
    {
        if (!await GuardBotAsync(GuildPermission.BanMembers))
            return;

        try
        {
            var pruneDays = Math.Clamp(dias, 0, 7);
            await Context.Guild.AddBanAsync(usuario.Id, pruneDays, motivo);
            await _memberRepository.SetBanAsync(Context.Guild.Id, usuario.Id, usuario.GetDisplayName(), true);
            await RespondAsync($"⛔ **{usuario.GetDisplayName()}** foi banido (apaga {pruneDays} dias de mensagens){(motivo != null ? $" — {motivo}" : "")}.");
        }
        catch (Exception ex)
        {
            await RespondAsync($"❌ Não foi possível banir: {ex.Message}");
        }
    }

    [SlashCommand("desbanir", "Remove o banimento de um usuário pelo ID")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task DesbanirAsync(
        [Summary("usuario_id", "ID do usuário banido")] string usuarioId)
    {
        if (!await GuardBotAsync(GuildPermission.BanMembers))
            return;

        if (!ulong.TryParse(usuarioId, out var id))
        {
            await RespondAsync("❌ Informe o ID do usuário (ex: 123456789012345678).");
            return;
        }

        try
        {
            await Context.Guild.RemoveBanAsync(id);
            await RespondAsync($"✅ Usuário <@{id}> foi desbanido.");
        }
        catch (Exception ex)
        {
            await RespondAsync($"❌ Não foi possível desbanir: {ex.Message}");
        }
    }

    [SlashCommand("banimentos", "Lista os usuários banidos do servidor")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task BanimentosAsync()
    {
        var bans = (await Context.Guild.GetBansAsync().FlattenAsync()).ToList();

        if (bans.Count == 0)
        {
            await RespondAsync("📭 Nenhum usuário banido neste servidor.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var ban in bans.Take(30))
            sb.AppendLine($"`{ban.User.Id}` **{ban.User.Username}** — {(string.IsNullOrEmpty(ban.Reason) ? "sem motivo" : ban.Reason)}");

        var embed = new EmbedBuilder()
            .WithTitle($"🚫 Banimentos ({bans.Count})")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter(bans.Count > 30
                ? $"...e mais {bans.Count - 30} banidos"
                : "Use /moderacao banir para banir")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("timeout", "Aplica ou remove um timeout em um membro (minutos)")]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    public async Task TimeoutAsync(
        [Summary("usuário", "Membro a silenciar")] IUser usuario,
        [Summary("minutos", "Duração em minutos (0 remove o timeout)")] int minutos,
        [Summary("motivo", "Motivo do timeout")] string? motivo = null)
    {
        if (Context.Guild.GetUser(usuario.Id) is not IGuildUser guildUser)
        {
            await RespondAsync("❌ Esse usuário não está no servidor.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.ModerateMembers))
            return;

        try
        {
            await guildUser.ModifyAsync(m => m.TimedOutUntil = minutos <= 0 ? null : DateTimeOffset.UtcNow.AddMinutes(minutos));

            await RespondAsync(minutos <= 0
                ? $"✅ Timeout removido de **{usuario.GetDisplayName()}**."
                : $"🤫 **{usuario.GetDisplayName()}** em timeout por {minutos} min{(motivo != null ? $" — {motivo}" : "")}.");
        }
        catch (Exception ex)
        {
            await RespondAsync($"❌ Não foi possível aplicar o timeout: {ex.Message}");
        }
    }

    [SlashCommand("avisar", "Aplica um aviso registrado a um membro")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task AvisarAsync(
        [Summary("usuário", "Membro a avisar")] IUser usuario,
        [Summary("motivo", "Motivo do aviso")] string? motivo = null)
    {
        var warning = new UserWarning
        {
            Reason = string.IsNullOrWhiteSpace(motivo) ? "Sem motivo informado" : motivo.Trim(),
            AddedBy = Context.User.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _memberRepository.AddWarningAsync(Context.Guild.Id, usuario.Id, usuario.GetDisplayName(), warning);

        var member = await _memberRepository.GetAsync(Context.Guild.Id, usuario.Id);
        var total = member?.Warnings.Count ?? 1;

        await RespondAsync($"🔨 Aviso **#{total}** aplicado a **{usuario.GetDisplayName()}** — **{warning.Reason}**");
    }

    [SlashCommand("avisos", "Lista os avisos de um membro (ou o resumo do servidor)")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task AvisosAsync(
        [Summary("usuário", "Membro específico (opcional)")] IUser? usuario = null)
    {
        if (usuario != null)
        {
            var member = await _memberRepository.GetAsync(Context.Guild.Id, usuario.Id);
            if (member == null || member.Warnings.Count == 0)
            {
                await RespondAsync($"📭 **{usuario.GetDisplayName()}** não tem avisos.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var warning in member.Warnings)
                sb.AppendLine($"`{warning.Id}` **{warning.Reason}** · por <@{warning.AddedBy}> · {warning.CreatedAt:dd/MM/yyyy HH:mm}");

            var embed = new EmbedBuilder()
                .WithTitle($"🔨 Avisos de {usuario.GetDisplayName()} ({member.Warnings.Count})")
                .WithGoldTheme()
                .WithDescription(sb.ToString())
                .WithStandardFooter($"Total: {member.Warnings.Count} avisos · use /moderacao removeaviso")
                .Build();

            await RespondAsync(embed: embed);
            return;
        }

        var members = (await _memberRepository.GetAllAsync(Context.Guild.Id))
            .Where(m => m.Warnings.Count > 0)
            .OrderByDescending(m => m.Warnings.Count)
            .ToList();

        if (members.Count == 0)
        {
            await RespondAsync("📭 Nenhum aviso registrado neste servidor.");
            return;
        }

        var summary = new StringBuilder();
        foreach (var member in members.Take(15))
            summary.AppendLine($"**{member.Username}** — {member.Warnings.Count} aviso(s)");

        var embedSummary = new EmbedBuilder()
            .WithTitle($"🔨 Avisos por membro ({members.Count} com avisos)")
            .WithGoldTheme()
            .WithDescription(summary.ToString())
            .WithStandardFooter(members.Count > 15
                ? $"...e mais {members.Count - 15} membros"
                : $"Total de avisos: {members.Sum(m => m.Warnings.Count)}")
            .Build();

        await RespondAsync(embed: embedSummary);
    }

    [SlashCommand("removeaviso", "Remove um aviso de um membro pelo ID")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task RemoveAvisoAsync(
        [Summary("usuário", "Membro dono do aviso")] IUser usuario,
        [Summary("aviso_id", "ID do aviso (visto em /moderacao avisos)")] string avisoId)
    {
        var removed = await _memberRepository.RemoveWarningAsync(Context.Guild.Id, usuario.Id, avisoId);
        await RespondAsync(removed
            ? $"✅ Aviso `{avisoId}` removido de **{usuario.GetDisplayName()}**."
            : $"❌ Aviso `{avisoId}` não encontrado para **{usuario.GetDisplayName()}**.");
    }

    [SlashCommand("ritmolento", "Define o ritmo lento (slowmode) do canal")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task RitmoLentoAsync(
        [Summary("segundos", "Intervalo em segundo (0 desativa)")] int segundos,
        [Summary("canal", "Canal (padrão: atual)")] ITextChannel? canal = null)
    {
        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await RespondAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.ManageChannels))
            return;

        await target.ModifyAsync(c => c.SlowModeInterval = Math.Clamp(segundos, 0, 21600));
        await RespondAsync($"🐌 Ritmo lento de **{segundos} seg** definido em {target.Mention}.");
    }

    [SlashCommand("trancar", "Bloqueia o envio de mensagens no canal")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task TrancarAsync(
        [Summary("canal", "Canal (padrão: atual)")] ITextChannel? canal = null)
    {
        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await RespondAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.ManageChannels))
            return;

        await target.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Deny));

        await RespondAsync($"🔒 Canal {target.Mention} trancado.");
    }

    [SlashCommand("destrancar", "Libera o envio de mensagens no canal")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task DestrancarAsync(
        [Summary("canal", "Canal (padrão: atual)")] ITextChannel? canal = null)
    {
        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await RespondAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        if (!await GuardBotAsync(GuildPermission.ManageChannels))
            return;

        await target.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Allow));

        await RespondAsync($"🔓 Canal {target.Mention} destrancado.");
    }

    private Task<bool> GuardBotAsync(GuildPermission permission)
        => CommandGuards.GuardBotPermissionAsync(Context, permission);
}