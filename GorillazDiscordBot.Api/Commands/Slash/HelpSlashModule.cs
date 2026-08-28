using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Commands.Slash;

public class HelpSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ajuda", "Mostra a central de ajuda com todas as categorias de comandos")]
    public async Task AjudaAsync()
    {
        await RespondAsync(
            embed: CommandCatalog.BuildOverviewEmbed(Context.Client.CurrentUser),
            components: CommandCatalog.BuildSelectMenu(Context.User.Id));
    }

    [ComponentInteraction("help:*", true)]
    public async Task HelpCategoryAsync(string invokerId)
    {
        await DeferAsync();

        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await FollowupAsync("📖 Use `/ajuda` para abrir sua própria central de ajuda.", ephemeral: true);
            return;
        }

        var component = (SocketMessageComponent)Context.Interaction;
        var categoryId = component.Data.Values.FirstOrDefault();

        var embed = string.IsNullOrEmpty(categoryId)
            ? CommandCatalog.BuildOverviewEmbed(Context.Client.CurrentUser)
            : CommandCatalog.BuildCategoryEmbed(categoryId, Context.Client.CurrentUser);

        await component.ModifyOriginalResponseAsync(m => m.Embed = embed);
    }
}
