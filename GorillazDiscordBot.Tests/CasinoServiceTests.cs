using Discord;
using FluentAssertions;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Services;
using NSubstitute;
using Xunit;
using static GorillazDiscordBot.Commands.Slash.CasinoSlashModule;

namespace GorillazDiscordBot.Tests;

public class CasinoServiceTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();

    [Fact]
    public void ValidateAmount_RejeitaValoresNaoPositivos()
    {
        CasinoService.ValidateAmount(0).valid.Should().BeFalse();
        CasinoService.ValidateAmount(-5).valid.Should().BeFalse();
        CasinoService.ValidateAmount(10).valid.Should().BeTrue();
    }

    [Fact]
    public async Task ReserveAsync_SaldoInsuficiente_RetornaErro()
    {
        _repo.TryDeductMoneyAsync(Arg.Any<ulong>(), Arg.Any<int>()).Returns((false, 0));

        var casino = new CasinoService(_repo);
        var result = await casino.ReserveAsync(1ul, 100);

        result.deducted.Should().BeFalse();
        result.error.Should().NotBeNull();
    }

    [Fact]
    public async Task ReserveAsync_SaldoOk_DeduzValor()
    {
        _repo.TryDeductMoneyAsync(Arg.Any<ulong>(), Arg.Any<int>()).Returns((true, 400));

        var casino = new CasinoService(_repo);
        var result = await casino.ReserveAsync(1ul, 100);

        result.deducted.Should().BeTrue();
        result.balance.Should().Be(400);
        await _repo.Received().TryDeductMoneyAsync(1ul, 100);
    }

    [Fact]
    public async Task CreditAsync_AdicionaMoedas()
    {
        var casino = new CasinoService(_repo);

        await casino.CreditAsync(1ul, 250);

        await _repo.Received().AddMoneyAsync(1ul, 250);
    }

    [Fact]
    public async Task PlaceSimpleBetAsync_SaldoInsuficiente_RetornaFalha()
    {
        _repo.TryDeductMoneyAsync(Arg.Any<ulong>(), Arg.Any<int>()).Returns((false, 0));

        var casino = new CasinoService(_repo);
        var result = await casino.PlaceSimpleBetAsync(1ul, 100);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNull();
        await _repo.DidNotReceive().AddMoneyAsync(Arg.Any<ulong>(), Arg.Any<int>());
    }

    [Fact]
    public async Task PlaceSimpleBetAsync_ApostaValida_PagaOuPerde()
    {
        _repo.TryDeductMoneyAsync(Arg.Any<ulong>(), Arg.Any<int>()).Returns((true, 0));

        var casino = new CasinoService(_repo);
        bool sawWin = false, sawLoss = false;

        for (int i = 0; i < 500; i++)
        {
            var r = await casino.PlaceSimpleBetAsync(1ul, 100);
            r.Success.Should().BeTrue();

            if (r.Won)
            {
                sawWin = true;
                r.Balance.Should().Be(200);
            }
            else
            {
                sawLoss = true;
                r.Balance.Should().Be(0);
            }
        }

        sawWin.Should().BeTrue();
        sawLoss.Should().BeTrue();
    }

    public class CasinoTableBuilderTests
    {
        private static IReadOnlyList<ButtonComponent> Buttons(MessageComponent component) =>
            component.Components
                .OfType<ActionRowComponent>()
                .SelectMany(row => row.Components)
                .OfType<ButtonComponent>()
                .ToList();

        [Fact]
        public void BuildReplayComponents_GeraCustomIdsCorretos()
        {
            var buttons = Buttons(CasinoTableBuilder.BuildReplayComponents("slots", "100"));

            buttons.Should().HaveCount(2);
            buttons[0].CustomId.Should().Be("casino:again:slots:100");
            buttons[1].CustomId.Should().Be("casino:allin:slots:100");
        }

        [Fact]
        public void BuildReplayComponents_Roleta_PreservaParametros()
        {
            var buttons = Buttons(CasinoTableBuilder.BuildReplayComponents("roleta", "vermelho:250:-1"));

            buttons[0].CustomId.Should().Be("casino:again:roleta:vermelho:250:-1");
            buttons[1].CustomId.Should().Be("casino:allin:roleta:vermelho:250:-1");
        }

        [Theory]
        [InlineData("again:slots:100", "again", "slots", new[] { "100" })]
        [InlineData("allin:roleta:vermelho:250:-1", "allin", "roleta", new[] { "vermelho", "250", "-1" })]
        [InlineData("again:bj:75", "again", "bj", new[] { "75" })]
        public void Decode_ReconstróiModoEParametros(string data, string mode, string gameKey, string[] pars)
        {
            var (decodedMode, decodedGame, decodedParams) = CasinoTableBuilder.Decode(data);

            decodedMode.Should().Be(mode);
            decodedGame.Should().Be(gameKey);
            decodedParams.Should().BeEquivalentTo(pars);
        }
    }

}