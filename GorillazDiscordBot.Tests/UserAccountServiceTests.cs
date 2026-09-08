using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using GorillazDiscordBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GorillazDiscordBot.Tests;

public class UserAccountServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEconomyRepository _economy = Substitute.For<IEconomyRepository>();
    private readonly IShopRepository _shop = Substitute.For<IShopRepository>();

    private UserAccountService CreateService()
        => new(_users, _economy, _shop, NullLogger<UserAccountService>.Instance);

    [Fact]
    public async Task StartSelfLink_MesmaConta_Falha()
    {
        var service = CreateService();

        var result = await service.StartSelfLinkAsync(1, 1);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StartSelfLink_MainJaVinculado_Falha()
    {
        _users.GetMainIdAsync(1).Returns(9UL);
        var service = CreateService();

        var result = await service.StartSelfLinkAsync(1, 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StartSelfLink_AltJaVinculado_Falha()
    {
        _users.GetMainIdAsync(1).Returns(1UL);
        _users.GetMainIdAsync(2).Returns(9UL);
        var service = CreateService();

        var result = await service.StartSelfLinkAsync(1, 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task StartSelfLink_Valido_RetornaCodigo()
    {
        _users.GetMainIdAsync(1).Returns(1UL);
        _users.GetMainIdAsync(2).Returns(2UL);
        var service = CreateService();

        var result = await service.StartSelfLinkAsync(1, 2);

        result.Success.Should().BeTrue();
        result.Code.Should().NotBeNull();
    }

    [Fact]
    public async Task Confirmar_RequerenteNaoEhAlt_Falha()
    {
        _users.GetMainIdAsync(1).Returns(1UL);
        _users.GetMainIdAsync(2).Returns(2UL);
        var service = CreateService();
        var start = await service.StartSelfLinkAsync(1, 2);

        var result = await service.ConfirmAsync(3, start.Code!);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_Valido_UnificaEconomiaEvincula()
    {
        _users.GetMainIdAsync(1).Returns(1UL);
        _users.GetMainIdAsync(2).Returns(2UL);
        _economy.UnifyProfileAsync(2, 1).Returns(new UnifyResult(10, 20, 30));
        _users.LinkAsync(1, 2).Returns(true);
        var service = CreateService();
        var start = await service.StartSelfLinkAsync(1, 2);

        var result = await service.ConfirmAsync(2, start.Code!);

        result.Success.Should().BeTrue();
        await _economy.Received(1).UnifyProfileAsync(2, 1);
        await _shop.Received(1).MigrateInventoryAsync(2, 1);
        await _users.Received(1).LinkAsync(1, 2);
    }

    [Fact]
    public async Task Confirmar_Valido_CodigoConsumido()
    {
        _users.GetMainIdAsync(1).Returns(1UL);
        _users.GetMainIdAsync(2).Returns(2UL);
        _economy.UnifyProfileAsync(2, 1).Returns(new UnifyResult(1, 2, 3));
        _users.LinkAsync(1, 2).Returns(true);
        var service = CreateService();
        var start = await service.StartSelfLinkAsync(1, 2);

        await service.ConfirmAsync(2, start.Code!);
        var second = await service.ConfirmAsync(2, start.Code!);

        second.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ForceLink_MainJaVinculado_Falha()
    {
        _users.GetMainIdAsync(1).Returns(9UL);
        var service = CreateService();

        var result = await service.ForceLinkAsync(1, 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Desvincular_SemVinculo_Falha()
    {
        _users.GetAsync(2).Returns((DiscordUserProfile?)null);
        var service = CreateService();

        var result = await service.UnlinkAsync(1, 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Desvincular_RequerenteNaoDono_Falha()
    {
        _users.GetAsync(2).Returns(new DiscordUserProfile { UserId = 2, MainUserId = 1 });
        _users.GetGroupAsync(2).Returns(new List<DiscordUserProfile>
        {
            new() { UserId = 1, MainUserId = 0 },
            new() { UserId = 2, MainUserId = 1 }
        });
        var service = CreateService();

        var result = await service.UnlinkAsync(3, 2);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Desvincular_PrincipalSemStaff_Falha()
    {
        _users.GetAsync(1).Returns(new DiscordUserProfile { UserId = 1, MainUserId = 0 });
        _users.GetGroupAsync(1).Returns(new List<DiscordUserProfile>
        {
            new() { UserId = 1, MainUserId = 0 },
            new() { UserId = 2, MainUserId = 1 }
        });
        var service = CreateService();

        var result = await service.UnlinkAsync(1, 1);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Desvincular_DonoRemoveAlt_Sucesso()
    {
        _users.GetAsync(2).Returns(new DiscordUserProfile { UserId = 2, MainUserId = 1 });
        _users.GetGroupAsync(2).Returns(new List<DiscordUserProfile>
        {
            new() { UserId = 1, MainUserId = 0 },
            new() { UserId = 2, MainUserId = 1 }
        });
        _users.UnlinkAsync(2).Returns(true);
        var service = CreateService();

        var result = await service.UnlinkAsync(1, 2);

        result.Success.Should().BeTrue();
        await _users.Received(1).UnlinkAsync(2);
    }

    [Fact]
    public async Task Desvincular_StaffDesfazGrupoInteiro()
    {
        _users.GetAsync(1).Returns(new DiscordUserProfile { UserId = 1, MainUserId = 0 });
        _users.GetGroupAsync(1).Returns(new List<DiscordUserProfile>
        {
            new() { UserId = 1, MainUserId = 0 },
            new() { UserId = 2, MainUserId = 1 },
            new() { UserId = 3, MainUserId = 1 }
        });
        _users.UnlinkAsync(2).Returns(true);
        _users.UnlinkAsync(3).Returns(true);
        var service = CreateService();

        var result = await service.UnlinkAsync(1, 1, staffBypass: true);

        result.Success.Should().BeTrue();
        await _users.Received(1).UnlinkAsync(2);
        await _users.Received(1).UnlinkAsync(3);
    }
}