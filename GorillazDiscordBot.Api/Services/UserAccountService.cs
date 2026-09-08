using System.Collections.Concurrent;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Services;

public class UserAccountService : IUserAccountService
{
    private static readonly TimeSpan ConfirmationWindow = TimeSpan.FromMinutes(5);

    private sealed record PendingLink(ulong MainUserId, ulong AltUserId, DateTime Expires);

    private readonly IUserRepository _users;
    private readonly IEconomyRepository _economy;
    private readonly ILogger<UserAccountService> _logger;
    private readonly ConcurrentDictionary<string, PendingLink> _pending = new();

    public UserAccountService(
        IUserRepository users,
        IEconomyRepository economy,
        ILogger<UserAccountService> logger)
    {
        _users = users;
        _economy = economy;
        _logger = logger;
    }

    public async Task<LinkStartResult> StartSelfLinkAsync(ulong mainId, ulong altId)
    {
        var validation = await ValidateAsync(mainId, altId);
        if (!validation.Success)
            return validation;

        string code;
        do
        {
            code = Random.Shared.Next(100000, 1000000).ToString();
        }
        while (_pending.ContainsKey(code));

        _pending[code] = new PendingLink(mainId, altId, DateTime.UtcNow.Add(ConfirmationWindow));
        return new LinkStartResult(true, "Confirmação pendente.", code);
    }

    public async Task<LinkCompletionResult> ConfirmAsync(ulong requesterId, string code)
    {
        var pending = TakePending(code);
        if (pending == null)
            return new LinkCompletionResult(false, "❌ Código de confirmação inválido ou expirado.");

        if (pending.AltUserId != requesterId)
            return new LinkCompletionResult(false, "❌ Apenas a conta que recebeu o código pode confirmar o vínculo.");

        return await CompleteLinkAsync(pending.MainUserId, pending.AltUserId);
    }

    public Task<LinkCompletionResult> ForceLinkAsync(ulong mainId, ulong altId)
        => CompleteLinkAsync(mainId, altId);

    public void CancelAsync(string code)
        => _pending.TryRemove(code, out _);

    public async Task<LinkCompletionResult> UnlinkAsync(ulong requesterId, ulong accountId, bool staffBypass = false)
    {
        var target = await _users.GetAsync(accountId);
        if (target == null)
            return new LinkCompletionResult(false, "❌ Conta não encontrada.");

        var mainId = target.MainUserId == 0 ? accountId : target.MainUserId;
        var group = await _users.GetGroupAsync(accountId);
        var alts = group.Where(m => m.UserId != mainId && m.MainUserId == mainId).ToList();

        if (alts.Count == 0)
            return new LinkCompletionResult(false, "❌ Essa conta não pertence a nenhum grupo vinculado.");

        if (accountId == mainId)
        {
            if (!staffBypass)
                return new LinkCompletionResult(false,
                    "❌ Essa conta é o principal do grupo. Use `contas forcar` (staff) para promover outra como principal ou remova os alts antes.");

            foreach (var alt in alts)
                await _users.UnlinkAsync(alt.UserId);

            return new LinkCompletionResult(true, "✅ Vínculos do grupo removidos.");
        }

        if (!staffBypass && requesterId != mainId && requesterId != accountId)
            return new LinkCompletionResult(false, "❌ Apenas o principal ou a própria conta pode ser desvinculada.");

        var removed = await _users.UnlinkAsync(accountId);
        return removed
            ? new LinkCompletionResult(true, "✅ Conta desvinculada. Os fundos são mantidos no principal.")
            : new LinkCompletionResult(false, "❌ Falha ao desvincular a conta. Tente novamente.");
    }

    private async Task<LinkStartResult> ValidateAsync(ulong mainId, ulong altId)
    {
        if (mainId == altId)
            return new LinkStartResult(false, "❌ Você não pode vincular uma conta a si mesmo.");

        var mainGroupId = await _users.GetMainIdAsync(mainId);
        if (mainGroupId != mainId)
            return new LinkStartResult(false, "❌ Sua conta já pertence a um grupo vinculado.");

        var altGroupId = await _users.GetMainIdAsync(altId);
        if (altGroupId != altId)
            return new LinkStartResult(false, "❌ Essa conta já pertence a outro grupo vinculado.");

        return new LinkStartResult(true, "OK");
    }

    private async Task<LinkCompletionResult> CompleteLinkAsync(ulong mainId, ulong altId)
    {
        var validation = await ValidateAsync(mainId, altId);
        if (!validation.Success)
            return new LinkCompletionResult(false, validation.Message);

        UnifyResult? merged;
        try
        {
            merged = await _economy.UnifyProfileAsync(altId, mainId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao unificar economia de {alt} em {main}", altId, mainId);
            return new LinkCompletionResult(false, "❌ Falha ao unificar a economia. Nada foi alterado.");
        }

        var linked = await _users.LinkAsync(mainId, altId);
        if (!linked)
            return new LinkCompletionResult(false, "❌ Falha ao registrar o vínculo. Tente novamente.");

        var message = merged == null
            ? "✅ Conta vinculada com sucesso!"
            : $"✅ Conta vinculada! Economia unificada: {merged.MergedMoney} na carteira, " +
              $"{merged.MergedBank} no banco e {merged.MergedSavings} na poupança.";

        return new LinkCompletionResult(true, message);
    }

    private PendingLink? TakePending(string code)
    {
        if (!_pending.TryGetValue(code, out var pending))
            return null;

        _pending.TryRemove(code, out _);

        if (pending.Expires < DateTime.UtcNow)
            return null;

        return pending;
    }
}