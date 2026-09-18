using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class ModerationModule : ModuleBase<SocketCommandContext>
{
    private readonly IGuildMemberRepository _memberRepository;

    public ModerationModule(IGuildMemberRepository memberRepository)
    {
        _memberRepository = memberRepository;
    }

    [Command("limpar")]
    [Summary("Apaga até N mensagens do canal (opcional: só de um usuário)")]
    public async Task LimparAsync([Remainder] string? args = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages, GuildPermission.ManageMessages))
            return;

        if (Context.Channel is not ITextChannel textChannel)
        {
            await ReplyAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        var (quantidade, usuario) = ParseLimparArgs(args);
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

        await ReplyAsync(embed: embed);
    }

    [Command("limpar-tudo")]
    [Summary("Recria o canal atual apagando todo o histórico")]
    public async Task LimparTudoAsync()
    {
        if (!await GuardAsync(GuildPermission.ManageMessages, GuildPermission.ManageChannels))
            return;
        if (!await CommandGuards.GuardBotPermissionAsync(Context, GuildPermission.ManageMessages))
            return;

        if (Context.Channel is not ITextChannel target)
        {
            await ReplyAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        var clone = await ChannelClone.CloneAsync(target);
        await target.DeleteAsync();

        await ((IMessageChannel)clone).SendMessageAsync($"🧹 Canal recriado por **{Context.User.GetDisplayName()}** — histórico apagado.");
    }

    [Command("expulsar")]
    [Summary("Expulsa um membro do servidor")]
    public async Task ExpulsarAsync(IGuildUser usuario, [Remainder] string? motivo = null)
    {
        if (!await GuardAsync(GuildPermission.KickMembers, GuildPermission.KickMembers))
            return;

        try
        {
            await usuario.KickAsync(motivo ?? "Sem motivo informado");
            await ReplyAsync($"👢 **{usuario.GetDisplayName()}** foi expulso{(motivo != null ? $" — {motivo}" : ".")}");
        }
        catch (Exception ex)
        {
            await ReplyAsync($"❌ Não foi possível expulsar: {ex.Message}");
        }
    }

    [Command("banir")]
    [Summary("Bane um membro do servidor")]
    public async Task BanirAsync(IGuildUser usuario, [Remainder] string? args = null)
    {
        if (!await GuardAsync(GuildPermission.BanMembers, GuildPermission.BanMembers))
            return;

        var (dias, motivo) = ParseBanArgs(args);

        try
        {
            await usuario.BanAsync(pruneDays: dias, reason: motivo);
            await _memberRepository.SetBanAsync(Context.Guild.Id, usuario.Id, usuario.GetDisplayName(), true);
            await ReplyAsync($"⛔ **{usuario.GetDisplayName()}** foi banido (apaga {dias} dias de mensagens){(motivo != null ? $" — {motivo}" : "")}.");
        }
        catch (Exception ex)
        {
            await ReplyAsync($"❌ Não foi possível banir: {ex.Message}");
        }
    }

    [Command("desbanir")]
    [Summary("Remove o banimento de um usuário pelo ID")]
    public async Task DesbanirAsync(string usuarioId)
    {
        if (!await GuardAsync(GuildPermission.BanMembers, GuildPermission.BanMembers))
            return;

        if (!ulong.TryParse(usuarioId, out var id))
        {
            await ReplyAsync("❌ Informe o ID do usuário (ex: `desbanir 123456789012345678`).");
            return;
        }

        try
        {
            await Context.Guild.RemoveBanAsync(id);
            await ReplyAsync($"✅ Usuário <@{id}> foi desbanido.");
        }
        catch (Exception ex)
        {
            await ReplyAsync($"❌ Não foi possível desbanir: {ex.Message}");
        }
    }

    [Command("banimentos")]
    [Summary("Lista os usuários banidos do servidor")]
    public async Task BanimentosAsync()
    {
        if (!await GuardAsync(GuildPermission.BanMembers))
            return;

        var bans = (await Context.Guild.GetBansAsync().FlattenAsync()).ToList();
        if (bans.Count == 0)
        {
            await ReplyAsync("📭 Nenhum usuário banido neste servidor.");
            return;
        }

        var sb = new StringBuilder();
        foreach (var ban in bans.Take(30))
            sb.AppendLine($"`{ban.User.Id}` **{ban.User.Username}** — {(string.IsNullOrEmpty(ban.Reason) ? "sem motivo" : ban.Reason)}");

        var embed = new EmbedBuilder()
            .WithTitle($"🚫 Banimentos ({bans.Count})")
            .WithGoldTheme()
            .WithDescription(sb.ToString())
            .WithStandardFooter(bans.Count > 30 ? $"...e mais {bans.Count - 30} banidos" : "Use /moderacao banir para banir")
            .Build();

        await ReplyAsync(embed: embed);
    }

    [Command("timeout")]
    [Summary("Aplica ou remove um timeout em um membro (minutos)")]
    public async Task TimeoutAsync(IGuildUser usuario, int minutos, [Remainder] string? motivo = null)
    {
        if (!await GuardAsync(GuildPermission.ModerateMembers, GuildPermission.ModerateMembers))
            return;

        try
        {
            await usuario.ModifyAsync(m => m.TimedOutUntil = minutos <= 0 ? null : DateTimeOffset.UtcNow.AddMinutes(minutos));

            await ReplyAsync(minutos <= 0
                ? $"✅ Timeout removido de **{usuario.GetDisplayName()}**."
                : $"🤫 **{usuario.GetDisplayName()}** em timeout por {minutos} min{(motivo != null ? $" — {motivo}" : "")}.");
        }
        catch (Exception ex)
        {
            await ReplyAsync($"❌ Não foi possível aplicar o timeout: {ex.Message}");
        }
    }

    [Command("avisar")]
    [Summary("Aplica um aviso registrado a um membro")]
    public async Task AvisarAsync(IGuildUser usuario, [Remainder] string? motivo = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages))
            return;

        var warning = new UserWarning
        {
            Reason = string.IsNullOrWhiteSpace(motivo) ? "Sem motivo informado" : motivo.Trim(),
            AddedBy = Context.User.Id,
            CreatedAt = DateTime.UtcNow
        };

        await _memberRepository.AddWarningAsync(Context.Guild.Id, usuario.Id, usuario.GetDisplayName(), warning);

        var member = await _memberRepository.GetAsync(Context.Guild.Id, usuario.Id);
        var total = member?.Warnings.Count ?? 1;

        await ReplyAsync($"🔨 Aviso **#{total}** aplicado a **{usuario.GetDisplayName()}** — **{warning.Reason}**");
    }

    [Command("avisos")]
    [Summary("Lista os avisos de um membro (ou o resumo do servidor)")]
    public async Task AvisosAsync(IGuildUser? usuario = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages))
            return;

        if (usuario != null)
        {
            var member = await _memberRepository.GetAsync(Context.Guild.Id, usuario.Id);
            if (member == null || member.Warnings.Count == 0)
            {
                await ReplyAsync($"📭 **{usuario.GetDisplayName()}** não tem avisos.");
                return;
            }

            var sb = new StringBuilder();
            foreach (var warning in member.Warnings)
                sb.AppendLine($"`{warning.Id}` **{warning.Reason}** · por <@{warning.AddedBy}> · {warning.CreatedAt:dd/MM/yyyy HH:mm}");

            var embed = new EmbedBuilder()
                .WithTitle($"🔨 Avisos de {usuario.GetDisplayName()} ({member.Warnings.Count})")
                .WithGoldTheme()
                .WithDescription(sb.ToString())
                .WithStandardFooter($"Total: {member.Warnings.Count} avisos · use removeaviso <id>")
                .Build();

            await ReplyAsync(embed: embed);
            return;
        }

        var members = (await _memberRepository.GetAllAsync(Context.Guild.Id))
            .Where(m => m.Warnings.Count > 0)
            .OrderByDescending(m => m.Warnings.Count)
            .ToList();

        if (members.Count == 0)
        {
            await ReplyAsync("📭 Nenhum aviso registrado neste servidor.");
            return;
        }

        var summary = new StringBuilder();
        foreach (var member in members.Take(15))
            summary.AppendLine($"**{member.Username}** — {member.Warnings.Count} aviso(s)");

        var embedSummary = new EmbedBuilder()
            .WithTitle($"🔨 Avisos por membro ({members.Count} com avisos)")
            .WithGoldTheme()
            .WithDescription(summary.ToString())
            .WithStandardFooter(members.Count > 15 ? $"...e mais {members.Count - 15} membros" : $"Total de avisos: {members.Sum(m => m.Warnings.Count)}")
            .Build();

        await ReplyAsync(embed: embedSummary);
    }

    [Command("removeaviso")]
    [Summary("Remove um aviso de um membro pelo ID")]
    public async Task RemoveAvisoAsync(IGuildUser usuario, string id)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages))
            return;

        var removed = await _memberRepository.RemoveWarningAsync(Context.Guild.Id, usuario.Id, id);
        await ReplyAsync(removed
            ? $"✅ Aviso `{id}` removido de **{usuario.GetDisplayName()}**."
            : $"❌ Aviso `{id}` não encontrado para **{usuario.GetDisplayName()}**.");
    }

    [Command("ritmolento")]
    [Summary("Define um intervalo de ritmo lento (slowmode) no canal")]
    public async Task RitmoLentoAsync(int segundos, ITextChannel? canal = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages, GuildPermission.ManageChannels))
            return;

        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        await target.ModifyAsync(c => c.SlowModeInterval = Math.Clamp(segundos, 0, 21600));
        await ReplyAsync($"🐌 Ritmo lento de **{segundos} seg** definido em {target.Mention}.");
    }

    [Command("trancar")]
    [Summary("Bloqueia o envio de mensagens no canal")]
    public async Task TrancarAsync(ITextChannel? canal = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages, GuildPermission.ManageChannels))
            return;

        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        await target.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Deny));

        await ReplyAsync($"🔒 Canal {target.Mention} trancado.");
    }

    [Command("destrancar")]
    [Summary("Libera o envio de mensagens no canal")]
    public async Task DestrancarAsync(ITextChannel? canal = null)
    {
        if (!await GuardAsync(GuildPermission.ManageMessages, GuildPermission.ManageChannels))
            return;

        var target = canal ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("❌ Este comando só funciona em canais de texto.");
            return;
        }

        await target.AddPermissionOverwriteAsync(
            Context.Guild.EveryoneRole,
            new OverwritePermissions(sendMessages: PermValue.Allow));

        await ReplyAsync($"🔓 Canal {target.Mention} destrancado.");
    }

    private async Task<bool> GuardAsync(GuildPermission userPermission, GuildPermission? botPermission = null)
    {
        if (!await CommandGuards.GuardGuildOnlyAsync(Context))
            return false;

        if (botPermission.HasValue && !await CommandGuards.GuardBotPermissionAsync(Context, botPermission.Value))
            return false;

        return await CommandGuards.GuardPermissionAsync(Context, userPermission);
    }

    private (int Quantidade, IGuildUser? Usuario) ParseLimparArgs(string? args)
    {
        var quantidade = 50;
        IGuildUser? usuario = null;

        if (string.IsNullOrWhiteSpace(args))
            return (quantidade, usuario);

        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (int.TryParse(tokens[0], out var parsed))
        {
            quantidade = parsed;
            if (tokens.Length > 1 && MentionUtils.TryParseUser(tokens[1], out var uid))
                usuario = Context.Guild.GetUser(uid);
        }
        else if (MentionUtils.TryParseUser(tokens[0], out var id))
        {
            usuario = Context.Guild.GetUser(id);
        }

        return (quantidade, usuario);
    }

    private static (int Dias, string? Motivo) ParseBanArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
            return (0, null);

        var tokens = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (int.TryParse(tokens[0], out var dias))
            return (Math.Clamp(dias, 0, 7), tokens.Length > 1 ? string.Join(' ', tokens.Skip(1)) : null);

        return (0, args.Trim());
    }
}