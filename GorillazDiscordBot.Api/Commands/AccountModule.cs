using Discord;
using Discord.Commands;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

[Group("contas")]
[RequireContext(ContextType.Guild)]
[Summary("Gerencia contas vinculadas (alt accounts)")]
public class AccountModule : ModuleBase<SocketCommandContext>
{
    private readonly IUserAccountService _service;
    private readonly IUserRepository _users;

    public AccountModule(IUserAccountService service, IUserRepository users)
    {
        _service = service;
        _users = users;
    }

    [Command]
    [Summary("Mostra suas contas vinculadas")]
    public async Task ContasAsync()
    {
        var group = await _users.GetGroupAsync(Context.User.Id);
        var mainId = group.FirstOrDefault()?.UserId ?? Context.User.Id;
        var alts = group.Where(m => m.UserId != mainId).ToList();

        if (alts.Count == 0)
        {
            await ReplyAsync("ℹ️ Você não tem contas vinculadas. Use `contas vincular <@alt>` para declarar uma alt.");
            return;
        }

        var sb = new System.Text.StringBuilder($"👥 **Grupo vinculado** (principal: **{await ResolveNameAsync(mainId)}**)\n\n");
        sb.AppendLine($"⭐ {mainId} — **{await ResolveNameAsync(mainId)}** (principal)");

        foreach (var alt in alts)
            sb.AppendLine($"🔗 {alt.UserId} — **{await ResolveNameAsync(alt.UserId)}**");

        sb.Append("\nA economia do grupo é unificada no principal.");
        await ReplyAsync(sb.ToString());
    }

    [Command("vincular")]
    [Summary("Declara uma alt sua (a alt recebe um código para confirmar)")]
    public async Task VincularAsync(IUser alt)
    {
        if (alt.IsBot)
        {
            await ReplyAsync("🤖 Não é possível vincular contas de bots.");
            return;
        }

        if (alt.Id == Context.User.Id)
        {
            await ReplyAsync("❌ Você não pode vincular uma conta a si mesmo.");
            return;
        }

        if (Context.Guild?.GetUser(alt.Id) == null)
        {
            await ReplyAsync("❌ A conta precisa estar neste servidor para ser vinculada.");
            return;
        }

        var result = await _service.StartSelfLinkAsync(Context.User.Id, alt.Id);
        if (!result.Success || result.Code == null)
        {
            await ReplyAsync(result.Message);
            return;
        }

        try
        {
            var dm = await alt.CreateDMChannelAsync();
            await dm.SendMessageAsync(
                $"👥 **{Context.User.GetDisplayName()}** pediu para vincular sua conta à dele.\n\n" +
                $"Para confirmar, digite no servidor: `contas confirmar {result.Code}`\n" +
                $"O código expira em **5 minutos**.");
        }
        catch (Exception)
        {
            _service.CancelAsync(result.Code);
            await ReplyAsync("❌ Não consegui enviar a confirmação por DM. Peça para a conta permitir DMs de membros do servidor e tente novamente.");
            return;
        }

        await ReplyAsync($"✅ Convite enviado para **{alt.GetDisplayName()}**! A confirmação expira em **5 minutos**.");
    }

    [Command("confirmar")]
    [Summary("Confirma o vínculo usando o código recebido por DM")]
    public async Task ConfirmarAsync(string codigo)
    {
        var result = await _service.ConfirmAsync(Context.User.Id, codigo.Trim());
        await ReplyAsync(result.Message);
    }

    [Command("desvincular")]
    [Summary("Remove uma conta do seu grupo (fundos ficam no principal)")]
    public async Task DesvincularAsync(IUser conta)
    {
        var result = await _service.UnlinkAsync(Context.User.Id, conta.Id);
        await ReplyAsync(result.Message);
    }

    [Command("forcar")]
    [Summary("Staff: vincula duas contas sem confirmação")]
    public async Task ForcarAsync(IUser main, IUser alt)
    {
        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await ReplyAsync(BotConstants.PermissionDenied);
            return;
        }

        if (main.IsBot || alt.IsBot)
        {
            await ReplyAsync("🤖 Não é possível vincular contas de bots.");
            return;
        }

        var result = await _service.ForceLinkAsync(main.Id, alt.Id);
        await ReplyAsync(result.Message);
    }

    [Command("tirar")]
    [Summary("Staff: remove uma conta de qualquer grupo")]
    public async Task TirarAsync(IUser conta)
    {
        if (!CommandGuards.HasManageGuildPermission(Context))
        {
            await ReplyAsync(BotConstants.PermissionDenied);
            return;
        }

        var result = await _service.UnlinkAsync(Context.User.Id, conta.Id, staffBypass: true);
        await ReplyAsync(result.Message);
    }

    private async Task<string> ResolveNameAsync(ulong userId)
    {
        var cached = Context.Client.GetUser(userId);
        var user = cached ?? await Context.Client.GetUserAsync(userId);
        return user?.GetDisplayName() ?? $"@{userId}";
    }
}