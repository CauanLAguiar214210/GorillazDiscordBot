using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Utils;

public static class GuildLogEmbeds
{
    private const int MaxContentLength = 1000;

    public static Embed MessageDeleted(string author, string? content, string channelName, ulong messageId)
    {
        return new EmbedBuilder()
            .WithTitle("🗑️ Mensagem apagada")
            .WithColor(Color.Orange)
            .AddField("Autor", string.IsNullOrWhiteSpace(author) ? BotConstants.Unknown : author, true)
            .AddField("Canal", channelName, true)
            .AddField("Conteúdo", string.IsNullOrWhiteSpace(content)
                ? BotConstants.NotAvailableInCache
                : Truncate(content))
            .AddField("ID", messageId.ToString(), true)
            .WithStandardFooter("Log de mensagens")
            .Build();
    }

    public static Embed MessageEdited(string author, string before, string after, string channelName)
    {
        return new EmbedBuilder()
            .WithTitle("✏️ Mensagem editada")
            .WithColor(Color.Gold)
            .AddField("Autor", string.IsNullOrWhiteSpace(author) ? BotConstants.Unknown : author, true)
            .AddField("Canal", channelName, true)
            .AddField("Antes", string.IsNullOrWhiteSpace(before) ? BotConstants.Unknown : Truncate(before))
            .AddField("Depois", string.IsNullOrWhiteSpace(after) ? BotConstants.Unknown : Truncate(after))
            .WithStandardFooter("Log de mensagens")
            .Build();
    }

    public static Embed UserBanned(string username, ulong userId)
    {
        return new EmbedBuilder()
            .WithTitle("🚫 Usuário banido")
            .WithColor(Color.Red)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed UserUnbanned(string username, ulong userId)
    {
        return new EmbedBuilder()
            .WithTitle("✅ Usuário desbanido")
            .WithColor(Color.Green)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed UserJoined(string username, int memberCount)
    {
        return new EmbedBuilder()
            .WithTitle("🟢 Membro entrou")
            .WithColor(Color.Green)
            .AddField("Usuário", username, true)
            .AddField("Membros", memberCount.ToString(), true)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    public static Embed UserLeft(string username, int memberCount)
    {
        return new EmbedBuilder()
            .WithTitle("🔴 Membro saiu")
            .WithColor(Color.Red)
            .AddField("Usuário", username, true)
            .AddField("Membros", memberCount.ToString(), true)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    private static string Truncate(string value)
        => value.Length <= MaxContentLength ? value : value[..MaxContentLength] + "…";
}
