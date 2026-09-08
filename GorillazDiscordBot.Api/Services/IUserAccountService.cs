namespace GorillazDiscordBot.Services;

public sealed record LinkStartResult(bool Success, string Message, string? Code = null);
public sealed record LinkCompletionResult(bool Success, string Message);

public interface IUserAccountService
{
    Task<LinkStartResult> StartSelfLinkAsync(ulong mainId, ulong altId);
    Task<LinkCompletionResult> ConfirmAsync(ulong requesterId, string code);
    Task<LinkCompletionResult> ForceLinkAsync(ulong mainId, ulong altId);
    Task<LinkCompletionResult> UnlinkAsync(ulong requesterId, ulong accountId, bool staffBypass = false);
    void CancelAsync(string code);
}