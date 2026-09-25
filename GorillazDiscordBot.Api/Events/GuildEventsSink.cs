using Discord;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;
using Microsoft.Extensions.Logging;

namespace GorillazDiscordBot.Events;

public class GuildEventsSink : BotEventSink
{
    private readonly ISettingsRepository<Guild> _guildRepository;
    private readonly IVoiceChannelService _voiceChannelService;
    private readonly IUserRepository _userRepository;
    private readonly IGuildMemberRepository _guildMemberRepository;
    private readonly IAltSanctionPolicy _sanctionPolicy;
    private readonly ILogger<GuildEventsSink> _logger;

    public GuildEventsSink(
        ISettingsRepository<Guild> guildRepository,
        IVoiceChannelService voiceChannelService,
        IUserRepository userRepository,
        IGuildMemberRepository guildMemberRepository,
        IAltSanctionPolicy sanctionPolicy,
        ILogger<GuildEventsSink> logger)
    {
        _guildRepository = guildRepository;
        _voiceChannelService = voiceChannelService;
        _userRepository = userRepository;
        _guildMemberRepository = guildMemberRepository;
        _sanctionPolicy = sanctionPolicy;
        _logger = logger;
    }

    public override Task OnGuildAvailableAsync(SocketGuild guild)
        => RefreshGuildInfoAsync(guild);

    public override Task OnGuildUpdatedAsync(SocketGuild before, SocketGuild after)
        => RefreshGuildInfoAsync(after);

    public override Task OnUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
        => _voiceChannelService.OnUserVoiceStateUpdatedAsync(user, before, after);

    public override async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var guild = await _guildRepository.GetAsync(user.Guild.Id);
            UpdateGuildInfo(guild, user.Guild);
            await _guildRepository.SaveAsync(guild);

            await EnforceGroupSanctionsAsync(user);

            if (!guild.Welcome.WelcomeEnabled || !guild.Welcome.WelcomeChannelId.HasValue)
                return;

            var channel = user.Guild.GetTextChannel(guild.Welcome.WelcomeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                guild.Welcome.WelcomeMessage,
                userMention: user.Mention,
                serverName: user.Guild.Name,
                memberCount: user.Guild.MemberCount);

            var embed = new EmbedBuilder()
                .WithTitle("🟢 Bem-vindo(a)!")
                .WithDescription(message)
                .WithColor(Color.Green)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithStandardFooter($"Membro nº {user.Guild.MemberCount}")
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar boas-vindas para {user} no servidor {guild}",
                user.GetDisplayName(), user.Guild.Name);
        }
    }

    public override async Task OnUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        try
        {
            var settings = await _guildRepository.GetAsync(guild.Id);
            UpdateGuildInfo(settings, guild);
            await _guildRepository.SaveAsync(settings);

            if (!settings.Welcome.GoodbyeEnabled || !settings.Welcome.GoodbyeChannelId.HasValue)
                return;

            var channel = guild.GetTextChannel(settings.Welcome.GoodbyeChannelId.Value);
            if (channel == null) return;

            var message = MessageTemplateResolver.Resolve(
                settings.Welcome.GoodbyeMessage,
                userMention: user.GetDisplayName(),
                serverName: guild.Name,
                memberCount: guild.MemberCount);

            var embed = new EmbedBuilder()
                .WithTitle("🔴 Adeus!")
                .WithDescription(message)
                .WithColor(Color.Red)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithStandardFooter($"Membros restantes: {guild.MemberCount}")
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar despedida para {user} no servidor {guild}",
                user.GetDisplayName(), guild.Name);
        }
    }

    private async Task RefreshGuildInfoAsync(SocketGuild socketGuild)
    {
        try
        {
            var guild = await _guildRepository.GetAsync(socketGuild.Id);
            UpdateGuildInfo(guild, socketGuild);
            await _guildRepository.SaveAsync(guild);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar informações do servidor {guild} ({guildId})",
                socketGuild.Name, socketGuild.Id);
        }
    }

    private static void UpdateGuildInfo(Guild guild, SocketGuild socketGuild)
    {
        guild.Info.Name = socketGuild.Name;
        guild.Info.IconUrl = socketGuild.IconUrl;
        guild.Info.OwnerId = socketGuild.OwnerId;
        guild.Info.OwnerName = socketGuild.Owner?.Username ?? guild.Info.OwnerName;
        guild.Info.MemberCount = socketGuild.MemberCount;
        guild.Info.BoostCount = socketGuild.PremiumSubscriptionCount;
        guild.Info.BoostLevel = (int)socketGuild.PremiumTier;
        guild.Info.JoinedAt ??= DateTime.UtcNow;
        guild.Info.CreatedAt ??= socketGuild.CreatedAt.UtcDateTime;
        guild.Info.PreferredLocale = socketGuild.PreferredLocale;
    }

    private async Task EnforceGroupSanctionsAsync(SocketGuildUser user)
    {
        try
        {
            var group = await _userRepository.GetGroupAsync(user.Id);
            if (group.Count <= 1) return;

            var memberIds = group.Select(m => m.UserId).ToArray();
            var members = await _guildMemberRepository.GetManyAsync(user.Guild.Id, memberIds);
            if (members.Count == 0) return;

            var states = members
                .Select(m => new AltMemberState(m.IsBanned, m.MuteUntil))
                .ToArray();
            var decision = _sanctionPolicy.Decide(states);

            if (decision.Action == AltSanctionAction.Ban)
            {
                await user.Guild.AddBanAsync(user.Id, 0, "Conta vinculada a um usuário banido no servidor");
                _logger.LogWarning("Banido {user} ao entrar — conta vinculada a banimento ativo na guild {guild}",
                    user.GetDisplayName(), user.Guild.Name);
                return;
            }

            if (decision.Action == AltSanctionAction.Timeout && decision.Timeout is { } remaining)
            {
                await user.SetTimeOutAsync(remaining);
                _logger.LogWarning("Aplicado timeout em {user} ao entrar — conta vinculada na guild {guild}",
                    user.GetDisplayName(), user.Guild.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao aplicar sanções de grupo para {user} no servidor {guild}",
                user.GetDisplayName(), user.Guild.Name);
        }
    }
}