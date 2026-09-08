using FluentAssertions;
using GorillazDiscordBot.Domain.Entity.Economy;
using GorillazDiscordBot.Services;
using GorillazDiscordBot.Utils;

namespace GorillazDiscordBot.Tests;

public class CasinoTableBuilderTests
{
    [Fact]
    public void DescribeAppliedRelic_SemBonus_RetornaNull()
    {
        var payout = new PayoutResult(100, 100, 0, null);

        CasinoTableBuilder.DescribeAppliedRelic(payout).Should().BeNull();
    }

    [Fact]
    public void DescribeAppliedRelic_GainBonus_DescrevePercentualEValor()
    {
        var relic = new AppliedRelic("Relógio do Cassino", "⌚", RelicEffect.GainBonus, 10);
        var payout = new PayoutResult(220, 100, 10, relic);

        var text = CasinoTableBuilder.DescribeAppliedRelic(payout);

        text.Should().NotBeNull();
        text.Should().Contain("Relógio do Cassino");
        text.Should().Contain("(+10%)");
        text.Should().Contain("+**10** moedas extras");
    }

    [Fact]
    public void DescribeAppliedRelic_Cashback_DescreveDevolucao()
    {
        var relic = new AppliedRelic("Relógio do Reembolso", "💸", RelicEffect.Cashback, 15);
        var payout = new PayoutResult(115, 0, 15, relic);

        var text = CasinoTableBuilder.DescribeAppliedRelic(payout);

        text.Should().NotBeNull();
        text.Should().Contain("Relógio do Reembolso");
        text.Should().Contain("(devolve 15% da aposta)");
        text.Should().Contain("+**15** moedas devolvidas");
    }
}