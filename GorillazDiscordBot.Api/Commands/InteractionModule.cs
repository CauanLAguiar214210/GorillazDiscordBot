using Discord.Commands;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands;

public class InteractionModule : ModuleBase<SocketCommandContext>
{
    [Command("interaction add")]
    [Summary("Comando movido para o slash")]
    public async Task InteractionAddAsync(string trigger, [Remainder] string response)
        => await RedirectToSlashAsync();

    [Command("interaction remove")]
    [Summary("Comando movido para o slash")]
    public async Task InteractionRemoveAsync(string trigger)
        => await RedirectToSlashAsync();

    [Command("interaction list")]
    [Summary("Comando movido para o slash")]
    public async Task InteractionListAsync()
        => await RedirectToSlashAsync();

    private async Task RedirectToSlashAsync()
        => await ReplyAsync("❌ Esse comando foi movido para o slash. Use `/interacao criar`, `/interacao remover` ou `/interacao listar`.");
}
