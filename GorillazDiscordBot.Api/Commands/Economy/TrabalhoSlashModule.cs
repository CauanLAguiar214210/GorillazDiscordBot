using System.Text;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Entity.Profile;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Api.Commands.Economy;

[Group("trabalho", "Trabalhe, tire diplomas e evolua sua carreira")]
public class TrabalhoSlashModule : InteractionModuleBase<SocketInteractionContext>
{
    private const string CustomIdPrefix = "trabalho";
    private const string ParkAction = "park";
    private const string FinishAction = "finish";
    private const string CatAction = "cat";
    private const string DomainAction = "vdomain";
    private const string JobSelAction = "job";
    private const string DoAction = "do";

    private readonly ICharacterProfileRepository _profiles;
    private readonly IEconomyRepository _economy;
    private readonly ShopService _shop;
    private readonly IEconomyAccessor _accessor;
    private readonly ManobristaSessionService _manobrista;

    public TrabalhoSlashModule(
        ICharacterProfileRepository profiles,
        IEconomyRepository economy,
        ShopService shop,
        IEconomyAccessor accessor,
        ManobristaSessionService manobrista)
    {
        _profiles = profiles;
        _economy = economy;
        _shop = shop;
        _accessor = accessor;
        _manobrista = manobrista;
    }

    // ──────────────────────────────────────────────────────────────
    // /trabalho listar — overview interativo com SelectMenu
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("listar", "Lista os trabalhos disponíveis por categoria")]
    public async Task ListarAsync()
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var ownedTypes = await _shop.GetOwnedVehicleTypesAsync(mainId);
        var manobVagas = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaVagas);
        var manobBasePct = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaBase);

        var embed = BuildJobOverviewEmbed(Context.User, profile, ownedTypes, manobVagas, manobBasePct);
        var components = BuildJobCategorySelectMenu(Context.User.Id);

        await RespondAsync(embed: embed, components: components);
    }

    // ──────────────────────────────────────────────────────────────
    // Selecao de categoria
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(CustomIdPrefix + ":" + CatAction + ":*", true)]
    public async Task CategorySelectAsync(string invokerId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("Use `/trabalho listar` para ver suas proprias opcoes.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var catId = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(catId)) return;

        if (catId == "back")
        {
            await ShowOverviewAsync(component, owner);
            return;
        }

        var category = ParseCategoryId(catId);
        if (category is null)
        {
            await FollowupAsync("Categoria invalida.", ephemeral: true);
            return;
        }

        if (category == JobCategory.Veiculo)
        {
            await ShowVehicleDomainMenuAsync(component, owner);
            return;
        }

        await ShowJobListAsync(component, owner, category.Value, domain: null);
    }

    // ──────────────────────────────────────────────────────────────
    // Selecao de dominio de veiculo (Terrestre / Aquatico / Aereo)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(CustomIdPrefix + ":" + DomainAction + ":*", true)]
    public async Task DomainSelectAsync(string invokerId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("Use `/trabalho listar` para ver suas proprias opcoes.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var value = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(value)) return;

        if (value == "back")
        {
            await ShowOverviewAsync(component, owner);
            return;
        }

        if (!Enum.TryParse<LicenseDomain>(value, ignoreCase: true, out var domain))
        {
            await FollowupAsync("Dominio invalido.", ephemeral: true);
            return;
        }

        await ShowJobListAsync(component, owner, JobCategory.Veiculo, domain);
    }

    // ──────────────────────────────────────────────────────────────
    // Selecao do trabalho dentro de uma categoria/dominio
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(CustomIdPrefix + ":" + JobSelAction + ":*:*", true)]
    public async Task JobSelectAsync(string invokerId, string catDomainId)
    {
        if (!ulong.TryParse(invokerId, out var owner) || owner != Context.User.Id)
        {
            await RespondAsync("Use `/trabalho listar` para ver suas proprias opcoes.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var component = (SocketMessageComponent)Context.Interaction;
        var jobKey = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(jobKey)) return;

        if (jobKey == "back")
        {
            var (backCat, backDomain) = ParseCatDomainId(catDomainId);
            if (backCat == JobCategory.Veiculo && backDomain is not null)
            {
                await ShowVehicleDomainMenuAsync(component, owner);
                return;
            }
            if (backCat is not null)
            {
                await ShowJobListAsync(component, owner, backCat.Value, backDomain);
                return;
            }
            await ShowOverviewAsync(component, owner);
            return;
        }

        var job = EconomyJobs.FindByKey(jobKey);
        if (job == null)
        {
            await FollowupAsync("Trabalho nao encontrado.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(owner);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var ownedTypes = await _shop.GetOwnedVehicleTypesAsync(mainId);
        var economy = await _economy.GetOrCreateAsync(mainId, Context.User.Username);
        var manobVagas = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaVagas);
        var manobBasePct = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaBase);

        var embed = BuildJobDetailEmbed(Context.User, job, profile, ownedTypes, economy, manobVagas, manobBasePct);
        var components = BuildJobDetailComponents(owner, job, profile, ownedTypes, catDomainId);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = components;
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Botao "Trabalhar" vindo do detalhe do trabalho
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(CustomIdPrefix + ":" + DoAction + ":*:*", true)]
    public async Task DoJobAsync(ulong ownerId, string jobKey)
    {
        if (ownerId != Context.User.Id)
        {
            await RespondAsync("Este botao nao e seu.", ephemeral: true);
            return;
        }

        var job = EconomyJobs.FindByKey(jobKey);
        if (job == null)
        {
            await RespondAsync("Trabalho nao encontrado.", ephemeral: true);
            return;
        }

        await ExecuteJobAsync(job);
    }

    // ──────────────────────────────────────────────────────────────
    // /trabalho trabalhar — atalho rapido por string (mantido)
    // ──────────────────────────────────────────────────────────────

    [SlashCommand("trabalhar", "Trabalha em uma profissao e recebe o pagamento")]
    public async Task TrabalharAsync(
        [Summary("profissao", "Chave ou nome da profissao (ex.: entregador, manobrista, piloto-aviao)")] string profissao)
    {
        var job = EconomyJobs.Find(profissao);
        if (job == null)
        {
            await RespondAsync("Profissao nao encontrada. Use `/trabalho listar` para ver as opcoes.", ephemeral: true);
            return;
        }

        await ExecuteJobAsync(job);
    }

    // ──────────────────────────────────────────────────────────────
    // Logica central de execucao do trabalho
    // ──────────────────────────────────────────────────────────────

    private async Task ExecuteJobAsync(Job job)
    {
        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        if (profile.Escolaridade < job.MinSchooling)
        {
            await RespondAsync(
                $"\U0001f393 Voce precisa de **{FormatSchooling(job.MinSchooling)}** para trabalhar como **{job.Name}**. Use `/ensino` para estudar.",
                ephemeral: true);
            return;
        }

        if (job.RequiresDiploma && !profile.Diplomas.Contains(job.Key))
        {
            await RespondAsync(
                $"\U0001f4dc Voce ainda nao tem o diploma de **{job.Name}**. Use `/trabalho prova {job.Key}` para tirar a licenca.",
                ephemeral: true);
            return;
        }

        if (job.RequiredLicense is { } license && !profile.Licencas.Contains(license))
        {
            await RespondAsync(
                $"\U0001f3ab Voce precisa da licenca **{VehicleRules.FormatRequirement(license)}** para trabalhar como **{job.Name}**. Use `/veiculo licenca prova`.",
                ephemeral: true);
            return;
        }

        if (job.PayMode == JobPayMode.Clicker)
        {
            await StartManobristaAsync(job, mainId);
            return;
        }

        var now = DateTime.UtcNow;
        var economy = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (EconomyRules.GetRemainingCooldown(economy.LastWorkTime, now, TimeSpan.FromHours(job.Hours)) is { } remaining)
        {
            await RespondAsync($"\u23f3 Voce ainda esta trabalhando! Espere {FormatRemaining(remaining)} para trabalhar como **{job.Name}**.", ephemeral: true);
            return;
        }

        var (pay, bonusPct) = await ComputePayAsync(job, mainId);

        var workBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Work);
        if (workBonus > 0)
            pay += pay * (ulong)workBonus / 100;

        var boost = economy.WorkBoostPending;
        if (boost && economy.WorkBoostExpiresAt is { } wExp && wExp <= DateTime.UtcNow)
        {
            await _economy.SetWorkBoostAsync(mainId, false);
            boost = false;
        }
        if (boost)
            pay *= 2;

        if (!await _economy.TryClaimWorkAsync(mainId, now, TimeSpan.FromHours(job.Hours)))
        {
            await RespondAsync("Voce ja esta trabalhando neste momento. Aguarde o termino para comecar outro servico.", ephemeral: true);
            return;
        }

        await _economy.AddMoneyAsync(
            mainId, pay, EconomyTransactionType.Work,
            $"Trabalhou como {job.Name} ({job.Hours}h)");

        if (boost)
            await _economy.SetWorkBoostAsync(mainId, false);

        var bonusSuffix = bonusPct > 0 ? $" (+{bonusPct}% particular)" : string.Empty;
        var boostSuffix = boost ? " \u26a1 **(com bonus x2!)**" : string.Empty;
        var petSuffix = workBonus > 0 ? $" \U0001f43e (+{workBonus}% pet)" : string.Empty;

        await RespondAsync($"\U0001f4aa Voce trabalhou como **{job.Emoji} {job.Name}** por {job.Hours}h e ganhou **{EconomyFormat.Full(pay)} moedas**!{bonusSuffix}{petSuffix}{boostSuffix}");
    }

    private async Task<(ulong pay, int bonusPct)> ComputePayAsync(Job job, ulong mainId)
    {
        var basePay = job.TotalPay;

        var bonusPct = 0;
        if (job.RequiredVehicleType is { } vehicleType)
        {
            var ownedTypes = await _shop.GetOwnedVehicleTypesAsync(mainId);
            if (ownedTypes.Contains(vehicleType))
                bonusPct = job.CategoryBonusPercent + job.TypeBonusPercent;
        }

        if (bonusPct > 0)
            basePay += basePay * (ulong)bonusPct / 100;

        return (basePay, bonusPct);
    }

    private async Task StartManobristaAsync(Job job, ulong mainId)
    {
        var now = DateTime.UtcNow;
        var economy = await _economy.GetOrCreateAsync(mainId, Context.User.Username);

        if (EconomyRules.GetRemainingCooldown(economy.LastWorkTime, now, TimeSpan.FromHours(job.Hours)) is { } remaining)
        {
            await RespondAsync($"\u23f3 Voce ainda esta trabalhando! Espere {FormatRemaining(remaining)} para abrir outro estacionamento.", ephemeral: true);
            return;
        }

        var vagas = ManobristaRules.Vagas + await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaVagas);
        var basePct = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaBase);
        var baseValue = (double)ManobristaRules.BasePerCar * (100 + basePct) / 100.0;

        if (!_manobrista.TryStart(mainId, vagas, baseValue, out var session))
        {
            await RespondAsync("\U0001f17f\ufe0f Voce ja tem um estacionamento aberto! Encerre o atual ou estacione mais carros.", ephemeral: true);
            return;
        }

        if (!await _economy.TryClaimWorkAsync(mainId, now, TimeSpan.FromHours(job.Hours)))
        {
            _manobrista.Remove(mainId);
            await RespondAsync("\u23f3 Voce ja esta trabalhando neste momento. Aguarde o termino para abrir outro servico.", ephemeral: true);
            return;
        }

        await RespondAsync(
            embed: BuildClickerEmbed(Context.User, session),
            components: BuildClickerComponents(Context.User.Id, session));
    }

    // ──────────────────────────────────────────────────────────────
    // Handlers do manobrista (clicker)
    // ──────────────────────────────────────────────────────────────

    [ComponentInteraction(CustomIdPrefix + ":" + ParkAction + ":*", true)]
    public async Task ParkAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Este estacionamento nao e seu.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _manobrista.Get(mainId);

        if (session == null || session.Finished)
        {
            await FollowupAsync("\U0001f17f\ufe0f Nenhum estacionamento aberto. Use `/trabalho trabalhar manobrista` para comecar outro.", ephemeral: true);
            return;
        }

        _manobrista.TryPark(mainId);
        var updated = _manobrista.Get(mainId)!;

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = BuildClickerEmbed(Context.User, updated);
            m.Components = BuildClickerComponents(Context.User.Id, updated);
        });
    }

    [ComponentInteraction(CustomIdPrefix + ":" + FinishAction + ":*", true)]
    public async Task FinishAsync(ulong ownerId)
    {
        await DeferAsync();

        if (ownerId != Context.User.Id)
        {
            await FollowupAsync("Este estacionamento nao e seu.", ephemeral: true);
            return;
        }

        var mainId = await _accessor.ResolveMainIdAsync(Context.User.Id);
        var session = _manobrista.Get(mainId);
        if (session == null || (session.Finished && session.CarrosEstacionados == 0))
        {
            await FollowupAsync("\U0001f17f\ufe0f Nenhum estacionamento aberto. Use `/trabalho trabalhar manobrista` para comecar outro.", ephemeral: true);
            return;
        }

        var parked = session.CarrosEstacionados;
        _manobrista.Remove(mainId);

        var pay = (ulong)Math.Round(session.TotalRaw);

        var workBonus = await _shop.GetUpgradePercentAsync(mainId, UpgradeEffect.Work);
        if (workBonus > 0)
            pay += pay * (ulong)workBonus / 100;

        var economy = await _economy.GetOrCreateAsync(mainId, Context.User.Username);
        var boost = economy.WorkBoostPending;
        if (boost && economy.WorkBoostExpiresAt is { } wExp && wExp <= DateTime.UtcNow)
        {
            await _economy.SetWorkBoostAsync(mainId, false);
            boost = false;
        }
        if (boost)
            pay *= 2;

        await _economy.AddMoneyAsync(
            mainId, pay, EconomyTransactionType.Work,
            $"Estacionou como Manobrista ({parked} carros)");

        if (boost)
            await _economy.SetWorkBoostAsync(mainId, false);

        var boostSuffix = boost ? " \u26a1 **(com bonus x2!)**" : string.Empty;
        var bonusSuffix = workBonus > 0 ? $" \U0001f43e (+{workBonus}% pet)" : string.Empty;

        await Context.Interaction.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = new EmbedBuilder()
                .WithGoldTheme()
                .WithAuthor($"{Context.User.GetDisplayName()} \u2014 Manobrista", Context.User.GetAvatarUrl())
                .WithTitle("\U0001f17f\ufe0f Servico encerrado")
                .WithDescription($"Voce estacionou **{parked} carro(s)** e recebeu **{EconomyFormat.Full(pay)} moedas**!{bonusSuffix}{boostSuffix}")
                .Build();
            m.Components = new ComponentBuilder().Build();
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Show helpers (navegacao interna)
    // ──────────────────────────────────────────────────────────────

    private async Task ShowOverviewAsync(SocketMessageComponent component, ulong ownerId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(ownerId);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var ownedTypes = await _shop.GetOwnedVehicleTypesAsync(mainId);
        var manobVagas = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaVagas);
        var manobBasePct = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaBase);

        var embed = BuildJobOverviewEmbed(Context.User, profile, ownedTypes, manobVagas, manobBasePct);
        var comps = BuildJobCategorySelectMenu(ownerId);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = comps;
        });
    }

    private async Task ShowVehicleDomainMenuAsync(SocketMessageComponent component, ulong ownerId)
    {
        var mainId = await _accessor.ResolveMainIdAsync(ownerId);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);

        var embed = BuildVehicleDomainEmbed(Context.User, profile);
        var comps = BuildVehicleDomainSelectMenu(ownerId);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = comps;
        });
    }

    private async Task ShowJobListAsync(
        SocketMessageComponent component, ulong ownerId,
        JobCategory category, LicenseDomain? domain)
    {
        var mainId = await _accessor.ResolveMainIdAsync(ownerId);
        var profile = await _profiles.GetOrCreateAsync(mainId, Context.User.Username);
        var ownedTypes = await _shop.GetOwnedVehicleTypesAsync(mainId);
        var manobVagas = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaVagas);
        var manobBasePct = await _shop.GetUpgradeFlatAsync(mainId, UpgradeEffect.ManobristaBase);

        var jobs = GetJobsForCategory(category, domain);
        var catDomainId = BuildCatDomainId(category, domain);

        var embed = BuildJobListEmbed(Context.User, category, domain, jobs, profile, ownedTypes, manobVagas, manobBasePct);
        var comps = BuildJobListSelectMenu(ownerId, catDomainId, jobs);

        await component.ModifyOriginalResponseAsync(m =>
        {
            m.Embed = embed;
            m.Components = comps;
        });
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de embed
    // ──────────────────────────────────────────────────────────────

    private static Embed BuildJobOverviewEmbed(
        IUser user, CharacterProfile profile, IReadOnlySet<VehicleType> ownedTypes,
        int manobVagas, int manobBasePct)
    {
        var sb = new StringBuilder();

        var subCount = EconomyJobs.SubEmpregos.Count;
        var subAvail = EconomyJobs.SubEmpregos.Count(j => IsUnlocked(j, profile, ownedTypes));
        sb.AppendLine($"\U0001f477 **Subempregos** \u2014 {subAvail}/{subCount} disponíveis \u00b7 sem diploma");
        sb.AppendLine();

        var empCount = EconomyJobs.Empregos.Count;
        var empAvail = EconomyJobs.Empregos.Count(j => IsUnlocked(j, profile, ownedTypes));
        sb.AppendLine($"\U0001f393 **Empregos** \u2014 {empAvail}/{empCount} disponíveis \u00b7 exigem diploma");
        sb.AppendLine();

        sb.AppendLine("\U0001f697 **Trabalhos com Veículos**");
        foreach (var domain in EconomyJobs.VehicleDomains)
        {
            var domJobs = EconomyJobs.VeiculosByDomain(domain);
            var avail = domJobs.Count(j => IsUnlocked(j, profile, ownedTypes));
            var (icon, label) = DomainMeta(domain);
            sb.AppendLine($"   {icon} **{label}** \u2014 {avail}/{domJobs.Count} disponíveis");
        }
        sb.AppendLine();
        sb.AppendLine("Selecione uma categoria abaixo ou use `/trabalho trabalhar <chave>` para trabalhar rápido.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Mercado de Trabalho", user.GetAvatarUrl())
            .WithTitle("\U0001f4bc Mercado de Trabalho")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Escolha uma categoria abaixo")
            .Build();
    }

    private static Embed BuildVehicleDomainEmbed(IUser user, CharacterProfile profile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Escolha um domínio para ver os trabalhos disponíveis:");
        sb.AppendLine();

        foreach (var domain in EconomyJobs.VehicleDomains)
        {
            var jobs = EconomyJobs.VeiculosByDomain(domain);
            var (icon, label) = DomainMeta(domain);
            sb.AppendLine($"{icon} **{label}** \u2014 {jobs.Count} trabalho(s)");
            foreach (var j in jobs)
                sb.AppendLine($"   \u2514 {j.Emoji} {j.Name} (`{j.Key}`)");
            sb.AppendLine();
        }

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Trabalhos com Veículos", user.GetAvatarUrl())
            .WithTitle("\U0001f697 Trabalhos com Veículos")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Escolha um domínio abaixo")
            .Build();
    }

    private static Embed BuildJobListEmbed(
        IUser user, JobCategory category, LicenseDomain? domain,
        IReadOnlyList<Job> jobs, CharacterProfile profile,
        IReadOnlySet<VehicleType> ownedTypes,
        int manobVagas, int manobBasePct)
    {
        var (icon, title) = CategoryMeta(category, domain);
        var sb = new StringBuilder();

        foreach (var job in jobs)
        {
            var status = Status(job, profile, ownedTypes, manobVagas, manobBasePct);
            sb.AppendLine($"{job.Emoji} **{job.Name}** (`{job.Key}`)");
            sb.AppendLine($"   \u2514 {status}");
            sb.AppendLine($"   \u2514 {job.Hours}h \u00b7 **{EconomyFormat.Full(job.TotalPay)}** moedas");
            if (job.PayMode == JobPayMode.Clicker)
            {
                var vagas = ManobristaRules.Vagas + manobVagas;
                var baseVal = (double)ManobristaRules.BasePerCar * (100 + manobBasePct) / 100.0;
                sb.AppendLine($"   \u2514 {vagas} vagas \u00b7 {EconomyFormat.Full((ulong)Math.Round(baseVal))}/carro (clique)");
            }
            sb.AppendLine();
        }

        if (jobs.Count == 0)
            sb.AppendLine("Nenhum trabalho nesta categoria no momento.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 {title}", user.GetAvatarUrl())
            .WithTitle($"{icon} {title}")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Selecione um trabalho abaixo")
            .Build();
    }

    private static Embed BuildJobDetailEmbed(
        IUser user, Job job, CharacterProfile profile,
        IReadOnlySet<VehicleType> ownedTypes,
        EconomyProfile economy, int manobVagas, int manobBasePct)
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Requisitos**");
        sb.AppendLine($"   \u2514 \U0001f393 Escolaridade: **{FormatSchooling(job.MinSchooling)}**");
        if (job.RequiresDiploma)
            sb.AppendLine($"   \u2514 \U0001f4dc Diploma de **{job.Name}** via `/trabalho prova {job.Key}`");
        if (job.RequiredLicense is { } lic)
            sb.AppendLine($"   \u2514 \U0001fa96 Licença: {VehicleRules.FormatRequirement(lic)}");
        if (job.RequiredVehicleType is { } vt)
            sb.AppendLine($"   \u2514 \U0001f697 Veículo: **{vt}** (+{job.CategoryBonusPercent + job.TypeBonusPercent}% com particular)");
        sb.AppendLine();

        sb.AppendLine("**Pagamento**");
        sb.AppendLine($"   \u2514 \u23f1\ufe0f Cooldown: **{job.Hours}h**");
        if (job.PayMode == JobPayMode.Clicker)
        {
            var vagas = ManobristaRules.Vagas + manobVagas;
            var baseVal = (double)ManobristaRules.BasePerCar * (100 + manobBasePct) / 100.0;
            sb.AppendLine($"   \u2514 \U0001f17f\ufe0f **{vagas} vagas** \u00b7 **{EconomyFormat.Full((ulong)Math.Round(baseVal))}** por carro");
        }
        else
        {
            sb.AppendLine($"   \u2514 \U0001f4b0 Fixo: **{EconomyFormat.Full(job.TotalPay)}** moedas");
            if (job.RequiredVehicleType is not null)
                sb.AppendLine($"   \u2514 +{job.CategoryBonusPercent + job.TypeBonusPercent}% com veículo particular");
        }
        sb.AppendLine();

        var statusLine = Status(job, profile, ownedTypes, manobVagas, manobBasePct);
        sb.AppendLine($"**Seu status:** {statusLine}");

        if (EconomyRules.GetRemainingCooldown(economy.LastWorkTime, DateTime.UtcNow, TimeSpan.FromHours(job.Hours)) is { } remaining)
            sb.AppendLine($"\u23f3 Em cooldown: **{FormatRemaining(remaining)}** restantes");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 {job.Name}", user.GetAvatarUrl())
            .WithTitle($"{job.Emoji} {job.Name}")
            .WithDescription(sb.ToString())
            .WithStandardFooter($"/trabalho trabalhar {job.Key}")
            .Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Builders de componentes
    // ──────────────────────────────────────────────────────────────

    private static MessageComponent BuildJobCategorySelectMenu(ulong userId)
    {
        var options = new List<SelectMenuOptionBuilder>
        {
            new("\U0001f477 Subempregos", "subemprego", "Sem diploma necessario"),
            new("\U0001f393 Empregos", "emprego", "Exigem diploma"),
            new("\U0001f697 Trabalhos com Veiculos", "veiculo", "Terrestres, aquaticos e aereos"),
        };

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{CustomIdPrefix}:{CatAction}:{userId}")
                .WithPlaceholder("Escolha uma categoria...")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildVehicleDomainSelectMenu(ulong userId)
    {
        var options = new List<SelectMenuOptionBuilder>
        {
            new("\U0001f4cb Voltar ao inicio", "back")
        };

        foreach (var domain in EconomyJobs.VehicleDomains)
        {
            var jobs = EconomyJobs.VeiculosByDomain(domain);
            var (icon, label) = DomainMeta(domain);
            options.Add(new SelectMenuOptionBuilder(
                $"{icon} {label} ({jobs.Count})",
                domain.ToString().ToLowerInvariant()));
        }

        if (!EconomyJobs.VehicleDomains.Contains(LicenseDomain.Maritima))
        {
            options.Add(new SelectMenuOptionBuilder(
                "\U0001f30a Aquaticos (em breve)",
                LicenseDomain.Maritima.ToString().ToLowerInvariant(),
                "Nenhum trabalho aquatico disponivel ainda"));
        }

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{CustomIdPrefix}:{DomainAction}:{userId}")
                .WithPlaceholder("Escolha um dominio de veiculo...")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildJobListSelectMenu(
        ulong userId, string catDomainId, IReadOnlyList<Job> jobs)
    {
        var options = new List<SelectMenuOptionBuilder>
        {
            new("\U0001f4cb Voltar", "back")
        };

        options.AddRange(jobs.Take(24).Select(j =>
            new SelectMenuOptionBuilder($"{j.Emoji} {j.Name}", j.Key, $"{j.Hours}h cooldown")));

        return new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
                .WithCustomId($"{CustomIdPrefix}:{JobSelAction}:{userId}:{catDomainId}")
                .WithPlaceholder("Selecione um trabalho...")
                .WithOptions(options))
            .Build();
    }

    private static MessageComponent BuildJobDetailComponents(
        ulong userId, Job job, CharacterProfile profile,
        IReadOnlySet<VehicleType> ownedTypes, string catDomainId)
    {
        var locked = IsLocked(job, profile, ownedTypes);
        var needsDiploma = job.RequiresDiploma && !profile.Diplomas.Contains(job.Key)
                           && profile.Escolaridade >= job.MinSchooling;

        var builder = new ComponentBuilder();

        if (needsDiploma)
        {
            builder.WithButton(new ButtonBuilder()
                .WithLabel("\U0001f4dc Fazer prova: /trabalho prova " + job.Key)
                .WithCustomId($"{CustomIdPrefix}:prova-hint:{userId}:{job.Key}")
                .WithStyle(ButtonStyle.Primary)
                .WithDisabled(true));
        }

        builder.WithButton(new ButtonBuilder()
            .WithLabel("\U0001f4aa Trabalhar")
            .WithCustomId($"{CustomIdPrefix}:{DoAction}:{userId}:{job.Key}")
            .WithStyle(ButtonStyle.Success)
            .WithDisabled(locked));

        builder.WithButton(new ButtonBuilder()
            .WithLabel("\u2b05 Voltar")
            .WithCustomId($"{CustomIdPrefix}:{JobSelAction}:{userId}:{catDomainId}")
            .WithStyle(ButtonStyle.Secondary));

        return builder.Build();
    }

    // ──────────────────────────────────────────────────────────────
    // Builders do manobrista (clicker)
    // ──────────────────────────────────────────────────────────────

    private static Embed BuildClickerEmbed(IUser user, ManobristaSession session)
    {
        var porCarro = (ulong)Math.Round(session.BaseValue);
        var total = (ulong)Math.Round(session.TotalRaw);
        var mult = ManobristaRules.ComboMultiplier(session.Combo);

        var sb = new StringBuilder();
        sb.AppendLine($"\U0001f697 Cada clique estaciona **1 carro** = **{EconomyFormat.Full(porCarro)} moedas**");
        if (session.Combo > 1)
            sb.AppendLine($"\U0001f525 **Combo \u00d7{mult:0.00}** \u2014 este clique vale **{EconomyFormat.Full((ulong)Math.Round(session.LastClickCoins))} moedas**");
        sb.AppendLine();
        var eventLine = BuildEventLine(session);
        if (eventLine.Length > 0)
            sb.AppendLine(eventLine);
        sb.AppendLine($"\U0001f17f\ufe0f **{session.CarrosEstacionados}/{session.Vagas} carros estacionados**");
        sb.AppendLine($"\U0001f4b0 Total acumulado: **{EconomyFormat.Full(total)} moedas**");
        if (session.IsFull)
            sb.AppendLine("\U0001f6a8 **Lotação cheia!** Encerre para receber o pagamento.");

        return new EmbedBuilder()
            .WithGoldTheme()
            .WithAuthor($"{user.GetDisplayName()} \u2014 Manobrista", user.GetAvatarUrl())
            .WithTitle("\U0001f17f\ufe0f Estacionamento")
            .WithDescription(sb.ToString())
            .WithStandardFooter("Clique para estacionar \u00b7 Encerre para receber")
            .Build();
    }

    private static string BuildEventLine(ManobristaSession session) => session.LastEvent switch
    {
        ManobristaEvent.Gorjeta => $"\U0001f4b5 **Gorjeta!** +{EconomyFormat.Full(ManobristaRules.GorjetaReward)} moedas",
        ManobristaEvent.Vip => $"\U0001f464 **Cliente VIP!** Este carro valeu **\u00d7{ManobristaRules.VipMultiplier:0}**",
        ManobristaEvent.Riscado => "\U0001fa93 **Carro riscado!** \u22121 carro estacionado",
        _ => string.Empty
    };

    private static MessageComponent BuildClickerComponents(ulong ownerId, ManobristaSession session)
    {
        var builder = new ComponentBuilder();

        if (!session.IsFull)
            builder.WithButton("\U0001f697 Estacionar (+1)", $"{CustomIdPrefix}:{ParkAction}:{ownerId}", ButtonStyle.Primary);

        builder.WithButton("\u2705 Encerrar e receber", $"{CustomIdPrefix}:{FinishAction}:{ownerId}", ButtonStyle.Success);

        return builder.Build();
    }

    // ──────────────────────────────────────────────────────────────
    // ──────────────────────────────────────────────────────────────
    // Utilitarios
    // ──────────────────────────────────────────────────────────────

    private static bool IsLocked(Job job, CharacterProfile profile, IReadOnlySet<VehicleType> ownedTypes)
    {
        if (profile.Escolaridade < job.MinSchooling) return true;
        if (job.RequiresDiploma && !profile.Diplomas.Contains(job.Key)) return true;
        if (job.RequiredLicense is { } lic && !profile.Licencas.Contains(lic)) return true;
        return false;
    }

    private static bool IsUnlocked(Job job, CharacterProfile profile, IReadOnlySet<VehicleType> ownedTypes)
        => !IsLocked(job, profile, ownedTypes);

    private static string Status(Job job, CharacterProfile profile, IReadOnlySet<VehicleType> ownedTypes, int manobVagas = 0, int manobBasePct = 0)
    {
        if (profile.Escolaridade < job.MinSchooling)
            return "\U0001f512 escolaridade insuficiente";
        if (job.RequiresDiploma && !profile.Diplomas.Contains(job.Key))
            return "\U0001f512 falta diploma";
        if (job.IsVeiculo)
        {
            if (job.RequiredLicense is { } lic && !profile.Licencas.Contains(lic))
                return $"\U0001f512 falta {VehicleRules.FormatRequirement(lic)}";
            if (job.PayMode == JobPayMode.Clicker)
            {
                var vagas = ManobristaRules.Vagas + manobVagas;
                var baseValue = (double)ManobristaRules.BasePerCar * (100 + manobBasePct) / 100.0;
                return $"\u2705 clique ({vagas} vagas \u00b7 {EconomyFormat.Full((ulong)Math.Round(baseValue))}/carro)";
            }
            if (job.RequiredVehicleType is { } vt && !ownedTypes.Contains(vt))
                return "\u2705 institucional (sem veículo para particular)";
            return "\u2705 disponível (+particular)";
        }
        return "\u2705 disponível";
    }

    private static IReadOnlyList<Job> GetJobsForCategory(JobCategory category, LicenseDomain? domain)
        => category switch
        {
            JobCategory.SubEmprego => EconomyJobs.SubEmpregos,
            JobCategory.Emprego => EconomyJobs.Empregos,
            JobCategory.Veiculo => domain is { } d
                ? EconomyJobs.VeiculosByDomain(d)
                : EconomyJobs.Veiculos,
            _ => Array.Empty<Job>()
        };

    private static string BuildCatDomainId(JobCategory category, LicenseDomain? domain)
        => category == JobCategory.Veiculo && domain is { } d
            ? $"veiculo-{d.ToString().ToLowerInvariant()}"
            : GetCategoryId(category);

    private static (JobCategory? Category, LicenseDomain? Domain) ParseCatDomainId(string id)
    {
        if (id.StartsWith("veiculo-", StringComparison.OrdinalIgnoreCase))
        {
            var domainStr = id["veiculo-".Length..];
            return Enum.TryParse<LicenseDomain>(domainStr, ignoreCase: true, out var d)
                ? (JobCategory.Veiculo, d)
                : (JobCategory.Veiculo, null);
        }
        return (ParseCategoryId(id), null);
    }

    private static string GetCategoryId(JobCategory category) => category switch
    {
        JobCategory.SubEmprego => "subemprego",
        JobCategory.Emprego => "emprego",
        JobCategory.Veiculo => "veiculo",
        _ => "other"
    };

    private static JobCategory? ParseCategoryId(string id) => id switch
    {
        "subemprego" => JobCategory.SubEmprego,
        "emprego" => JobCategory.Emprego,
        "veiculo" => JobCategory.Veiculo,
        _ => null
    };

    private static (string Icon, string Label) CategoryMeta(JobCategory category, LicenseDomain? domain)
        => category == JobCategory.Veiculo && domain is { } d
            ? DomainMeta(d)
            : category switch
            {
                JobCategory.SubEmprego => ("\U0001f477", "Subempregos"),
                JobCategory.Emprego => ("\U0001f393", "Empregos"),
                JobCategory.Veiculo => ("\U0001f697", "Trabalhos com Veículos"),
                _ => ("\U0001f4cb", "Trabalhos")
            };

    private static (string Icon, string Label) DomainMeta(LicenseDomain domain) => domain switch
    {
        LicenseDomain.Terrestre => ("\U0001f6e3\ufe0f", "Terrestres"),
        LicenseDomain.Maritima => ("\U0001f30a", "Aquáticos"),
        LicenseDomain.Aerea => ("\u2708\ufe0f", "Aéreos"),
        _ => ("\u2753", "Desconhecido")
    };

    private static char Letter(int index) => (char)('A' + index);

    private static string FormatSchooling(SchoolingLevel level) => level switch
    {
        SchoolingLevel.EnsinoFundamental1 => "Ensino Fundamental I",
        SchoolingLevel.EnsinoFundamental2 => "Ensino Fundamental II",
        SchoolingLevel.EnsinoMedio => "Ensino Médio",
        SchoolingLevel.EnsinoSuperior => "Ensino Superior",
        _ => "Nenhuma"
    };

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}min";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes}min";
        return $"{remaining.Seconds}seg";
    }
}
