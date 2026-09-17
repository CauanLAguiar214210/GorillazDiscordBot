using System.Text;
using Discord;
using Discord.Interactions;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Profile;

[Group("ensino", "Provas de matemática para evoluir sua escolaridade")]
public class EnsinoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string CustomIdPrefix = "ensino";
    private const string AnswerAction = "answer";
    private const string CancelAction = "cancel";
    private const string RetryAction = "retry";

    private readonly ICharacterProfileRepository _profiles;
    private readonly QuizSessionService _sessions;
    private readonly IEconomyAccessor _accessor;

    public EnsinoSlashModule(
        ICharacterProfileRepository profiles,
        QuizSessionService sessions,
        IEconomyAccessor accessor)
    {
        _profiles = profiles;
        _sessions = sessions;
        _accessor = accessor;
    }

    [SlashCommand("ver", "Mostra seu nível escolar e a próxima prova")]
    public async Task VerAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var next = SchoolingProgression.Next(profile.Escolaridade);

        var sb = new StringBuilder();
        sb.AppendLine($"🎓 **Nível atual:** {FormatLevel(profile.Escolaridade)}");
        sb.AppendLine();
        sb.AppendLine(next != null
            ? $"📝 Próxima prova: **{FormatLevel(next.Value)}** — use `/ensino prova`."
            : "🏆 Você concluiu todos os níveis de ensino!");

        var embed = new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{Context.User.GetDisplayName()} — Ensino", Context.User.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithStandardFooter("Acerte 2 de 3 questões para passar")
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("prova", "Faz a prova de matemática do próximo nível")]
    public async Task ProvaAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var next = SchoolingProgression.Next(profile.Escolaridade);

        if (next == null)
        {
            await RespondAsync("🏆 Você já concluiu todos os níveis de ensino!", ephemeral: true);
            return;
        }

        if (!_sessions.TryStart(mainId, next.Value, out var session))
        {
            await RespondAsync("📝 Você já tem uma prova em andamento! Termine ou cancele antes de começar outra.", ephemeral: true);
            return;
        }

        await RespondAsync(
            embed: BuildQuestionEmbed(Context.User, session),
            components: BuildQuestionComponents(Context.User.Id, session));
    }

    [ComponentInteraction(CustomIdPrefix + ":" + AnswerAction + ":*:*", true)]
    public async Task AnswerAsync(ulong ownerId, int optionIndex)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _sessions.Get(mainId);

        if (session == null || session.IsFinished)
        {
            await FollowupAsync("⌛ Esta prova expirou. Use `/ensino prova` para começar outra.", ephemeral: true);
            return;
        }

        if (session.Current.CorrectIndex == optionIndex)
            session.CorrectCount++;

        session.CurrentIndex++;

        if (!session.IsFinished)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(m =>
            {
                m.Embed = BuildQuestionEmbed(Context.User, session);
                m.Components = BuildQuestionComponents(Context.User.Id, session);
            });
            return;
        }

        var passed = session.Passed;
        if (passed)
        {
            var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
            profile.Escolaridade = session.Level;
            await _profiles.SaveAsync(profile);
        }

        _sessions.Remove(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildResultEmbed(Context.User, session, passed);
            m.Components = passed
                ? new ComponentBuilder().Build()
                : BuildRetryComponents(Context.User.Id, session.Level);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + RetryAction + ":*:*", true)]
    public async Task RetryAsync(ulong ownerId, int level)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _sessions.Remove(mainId);

        if (!_sessions.TryStart(mainId, (SchoolingLevel)level, out var session))
        {
            await FollowupAsync("📝 Você já tem uma prova em andamento.", ephemeral: true);
            return;
        }

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildQuestionEmbed(Context.User, session);
            m.Components = BuildQuestionComponents(Context.User.Id, session);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + CancelAction + ":*", true)]
    public async Task CancelAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("🚪 Esta prova não é sua.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        _sessions.Cancel(mainId);

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithGoldTheme()
                .WithDescription("🚪 Prova cancelada. Use `/ensino prova` quando quiser tentar de novo.")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    private static Embed BuildQuestionEmbed(IUser user, QuizSession session)
    {
        var question = session.Current;
        var sb = new StringBuilder();
        sb.AppendLine($"**{question.Text}**");
        sb.AppendLine();
        for (var i = 0; i < question.Options.Count; i++)
            sb.AppendLine($"`{Letter(i)}` {question.Options[i]}");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Matemática", user.GetAvatarUrl())
            .WithTitle($"🎓 {FormatLevel(session.Level)}")
            .WithDescription(sb.ToString())
            .WithFooter($"Questão {session.CurrentIndex + 1}/{session.Questions.Count} · Acerte {QuizSessionService.PassingScore} de {session.Questions.Count}")
            .Build();
    }

    private static MessageComponent BuildQuestionComponents(ulong ownerId, QuizSession session)
    {
        var question = session.Current;
        var builder = new ComponentBuilder();

        for (var i = 0; i < question.Options.Count && i < 4; i++)
            builder.WithButton(
                $"{Letter(i)}) {question.Options[i]}",
                $"{CustomIdPrefix}:{AnswerAction}:{ownerId}:{i}",
                ButtonStyle.Primary);

        builder.WithButton("❌ Cancelar", $"{CustomIdPrefix}:{CancelAction}:{ownerId}", ButtonStyle.Danger);
        return builder.Build();
    }

    private static MessageComponent BuildRetryComponents(ulong ownerId, SchoolingLevel level)
        => new ComponentBuilder()
            .WithButton("🔁 Tentar de novo", $"{CustomIdPrefix}:{RetryAction}:{ownerId}:{(int)level}", ButtonStyle.Success)
            .Build();

    private static Embed BuildResultEmbed(IUser user, QuizSession session, bool passed)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"✅ Acertos: **{session.CorrectCount}/{session.Questions.Count}**");
        sb.AppendLine();
        sb.AppendLine(passed
            ? $"🎉 **Aprovado!** Sua escolaridade agora é **{FormatLevel(session.Level)}**."
            : $"❌ **Reprovado.** Acerte {QuizSessionService.PassingScore} de {session.Questions.Count} para passar. Tente novamente!");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} — Prova de Matemática", user.GetAvatarUrl())
            .WithTitle(passed ? "🎓 Aprovado!" : "📝 Reprovado")
            .WithDescription(sb.ToString())
            .Build();
    }

    private static char Letter(int index) => (char)('A' + index);

    private static string FormatLevel(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Ensino Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Ensino Fundamental II",
        SchoolingLevel.EnsinoMedio => "Ensino Médio",
        SchoolingLevel.EnsinoSuperior => "Ensino Superior",
        _ => "Nenhuma"
    };
}