using Discord;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Services;

public static class GuildLogEmbeds
{
    private const int MaxContentLength = 1000;
    public const string Unknown = "Desconhecido(a)";

    public static Embed RaidDetected(int joinCount, int maxPerWindow)
    {
        return new EmbedBuilder()
            .WithTitle("🚨 Possível ataque (raid) detectado")
            .WithColor(Color.Red)
            .WithDescription($"**{joinCount} entradas** em menos de **{maxPerWindow} entradas por janela** — novos membros estão sendo banidos automaticamente.")
            .WithStandardFooter("Anti-raid ativo · configure em /anti-raid")
            .Build();
    }

    public static Embed MessageDeleted(string author, string? content, string channelName, ulong messageId)
    {
        return new EmbedBuilder()
            .WithTitle("🗑️ Mensagem apagada")
            .WithColor(Color.Orange)
            .AddField("Autor", string.IsNullOrWhiteSpace(author) ? Unknown : author, true)
            .AddField("Canal", channelName, true)
            .AddField("Mensagem", string.IsNullOrWhiteSpace(content)
                ? "Conteúdo não disponível (fora do cache)."
                : Truncate(content))
            .AddField("ID", messageId.ToString(), true)
            .WithStandardFooter("Log de mensagens")
            .Build();
    }

    public static Embed MessageEdited(string author, string beforeContent, string afterContent, string channelName)
    {
        return new EmbedBuilder()
            .WithTitle("✏️ Mensagem editada")
            .WithColor(Color.Orange)
            .AddField("Autor", string.IsNullOrWhiteSpace(author) ? Unknown : author, true)
            .AddField("Canal", channelName, true)
            .AddField("Antes", Truncate(string.IsNullOrWhiteSpace(beforeContent) ? "(vazia)" : beforeContent))
            .AddField("Depois", Truncate(string.IsNullOrWhiteSpace(afterContent) ? "(vazia)" : afterContent))
            .WithStandardFooter("Log de mensagens")
            .Build();
    }

    public static Embed MessagesBulkDeleted(int count, string channelName)
    {
        return new EmbedBuilder()
            .WithTitle("🧹 Mensagens apagadas em massa")
            .WithColor(Color.Orange)
            .AddField("Quantidade", count.ToString(), true)
            .AddField("Canal", channelName, true)
            .WithStandardFooter("Log de mensagens")
            .Build();
    }

    public static Embed UserBanned(
        string username,
        ulong userId,
        DateTimeOffset? accountCreated = null,
        bool isBot = false,
        string? avatarUrl = null,
        string? reason = null)
    {
        return BaseUserEmbed("⛔ Usuário banido", Color.Red, username, userId, accountCreated, isBot, avatarUrl, reason).Build();
    }

    public static Embed UserUnbanned(
        string username,
        ulong userId,
        DateTimeOffset? accountCreated = null,
        bool isBot = false,
        string? avatarUrl = null)
    {
        return BaseUserEmbed("✅ Usuário desbanido", Color.Green, username, userId, accountCreated, isBot, avatarUrl).Build();
    }

    public static Embed UserKicked(
        string username,
        ulong userId,
        DateTimeOffset? accountCreated = null,
        bool isBot = false,
        string? avatarUrl = null,
        string? reason = null)
    {
        return BaseUserEmbed("👢 Usuário expulso", Color.Orange, username, userId, accountCreated, isBot, avatarUrl, reason).Build();
    }

    public static Embed UserJoined(
        string username,
        int memberCount,
        DateTimeOffset? accountCreated = null,
        bool isBot = false,
        string? avatarUrl = null)
    {
        return BaseUserEmbed("🟢 Membro entrou", Color.Green, username, null, accountCreated, isBot, avatarUrl)
            .AddField("Membros", memberCount.ToString(), true)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    public static Embed UserLeft(
        string username,
        int memberCount,
        DateTimeOffset? accountCreated = null,
        bool isBot = false,
        string? avatarUrl = null)
    {
        return BaseUserEmbed("🔴 Membro saiu", Color.Red, username, null, accountCreated, isBot, avatarUrl)
            .AddField("Membros", memberCount.ToString(), true)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    public static Embed WarningCreated(string username, ulong userId, string reason, int total, ulong moderatorId)
    {
        return new EmbedBuilder()
            .WithTitle("🔨 Aviso aplicado")
            .WithColor(Color.Orange)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Aplicado por", $"<@{moderatorId}>", true)
            .AddField("Aviso #", total.ToString(), true)
            .AddField("Motivo", Truncate(string.IsNullOrWhiteSpace(reason) ? "Sem motivo informado" : reason), false)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed WarningRemoved(string username, ulong userId, string warningId, int total, ulong moderatorId)
    {
        return new EmbedBuilder()
            .WithTitle("♻️ Aviso removido")
            .WithColor(Color.Orange)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Removido por", $"<@{moderatorId}>", true)
            .AddField("Aviso ID", warningId, true)
            .AddField("Avisos restantes", total.ToString(), true)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed TimeoutApplied(string username, ulong userId, DateTimeOffset until)
    {
        return new EmbedBuilder()
            .WithTitle("🤫 Timeout aplicado")
            .WithColor(Color.Orange)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Até", $"{until:dd/MM/yyyy HH:mm} UTC", true)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed TimeoutRemoved(string username, ulong userId)
    {
        return new EmbedBuilder()
            .WithTitle("🎉 Timeout removido")
            .WithColor(Color.Green)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .WithStandardFooter("Log de moderação")
            .Build();
    }

    public static Embed NicknameChanged(string username, ulong userId, string? before, string after)
    {
        return new EmbedBuilder()
            .WithTitle("🏷️ Apelido alterado")
            .WithColor(Color.Blue)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Antes", string.IsNullOrWhiteSpace(before) ? "(nenhum)" : before, true)
            .AddField("Depois", string.IsNullOrWhiteSpace(after) ? "(nenhum)" : after, true)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    public static Embed RolesChanged(string username, ulong userId, IReadOnlyList<string> added, IReadOnlyList<string> removed)
    {
        return new EmbedBuilder()
            .WithTitle("🎭 Cargos alterados")
            .WithColor(Color.Blue)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Cargos adicionados", added.Count == 0 ? "—" : string.Join(", ", added), false)
            .AddField("Cargos removidos", removed.Count == 0 ? "—" : string.Join(", ", removed), false)
            .WithStandardFooter("Log de membros")
            .Build();
    }

    public static Embed VoiceStateChanged(string username, ulong userId, string? channelName, bool joined)
    {
        return new EmbedBuilder()
            .WithTitle(joined ? "🎙️ Entrou no canal de voz" : "🎙️ Saiu do canal de voz")
            .WithColor(Color.Blue)
            .AddField("Usuário", username, true)
            .AddField("ID", userId.ToString(), true)
            .AddField("Canal", string.IsNullOrWhiteSpace(channelName) ? "—" : channelName, true)
            .WithStandardFooter("Log de voz")
            .Build();
    }

    public static Embed ChannelCreated(string name, ulong id, string type)
    {
        return new EmbedBuilder()
            .WithTitle("📁 Canal criado")
            .WithColor(Color.Blue)
            .AddField("Nome", name, true)
            .AddField("ID", id.ToString(), true)
            .AddField("Tipo", type, true)
            .WithStandardFooter("Log de canais")
            .Build();
    }

    public static Embed ChannelDestroyed(string name, ulong id, string type)
    {
        return new EmbedBuilder()
            .WithTitle("🗑️ Canal removido")
            .WithColor(Color.Red)
            .AddField("Nome", name, true)
            .AddField("ID", id.ToString(), true)
            .AddField("Tipo", type, true)
            .WithStandardFooter("Log de canais")
            .Build();
    }

    public static Embed ChannelUpdated(string name, ulong id, string type)
    {
        return new EmbedBuilder()
            .WithTitle("✏️ Canal atualizado")
            .WithColor(Color.Blue)
            .AddField("Nome", name, true)
            .AddField("ID", id.ToString(), true)
            .AddField("Tipo", type, true)
            .WithStandardFooter("Log de canais")
            .Build();
    }

    private static EmbedBuilder BaseUserEmbed(
        string title,
        Color color,
        string username,
        ulong? userId,
        DateTimeOffset? accountCreated,
        bool isBot,
        string? avatarUrl,
        string? reason = null)
    {
        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color)
            .AddField("Usuário", string.IsNullOrWhiteSpace(username) ? Unknown : username, true);

        if (userId.HasValue)
            builder.AddField("ID", userId.Value.ToString(), true);
        if (avatarUrl != null)
            builder.WithThumbnailUrl(avatarUrl);
        if (accountCreated.HasValue)
            builder.AddField("Conta criada", $"{accountCreated.Value:dd/MM/yyyy}", true);
        builder.AddField("Bot", isBot ? "Sim" : "Não", true);
        if (reason != null)
            builder.AddField("Motivo", Truncate(string.IsNullOrWhiteSpace(reason) ? "Sem motivo informado" : reason), false);

        builder.WithStandardFooter("Log de moderação");
        return builder;
    }

    private static string Truncate(string value)
        => value.Length <= MaxContentLength ? value : value[..MaxContentLength] + "...";
}