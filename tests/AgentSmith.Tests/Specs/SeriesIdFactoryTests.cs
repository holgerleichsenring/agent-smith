using AgentSmith.Application.Services.Specs;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-06-03c7c: a series' base id is minted from the clock — the UTC date plus four random
/// hex digits — and every member is the base plus a letter, the shape the spec schema admits.
/// </summary>
public sealed class SeriesIdFactoryTests
{
    [Fact]
    public void SeriesIdFactory_Mint_IsDateHex()
    {
        var clock = new SettableClock { Now = new DateTimeOffset(2026, 10, 6, 23, 30, 0, TimeSpan.FromHours(-2)) };

        var minted = new SeriesIdFactory(clock).Mint();

        minted.Should().MatchRegex("^2026-10-07-[0-9a-f]{4}$", "the date is UTC, not the local one");
        PhaseIdSchemaTests.SpecPattern().IsMatch(SeriesIdFactory.Member(minted, 0)).Should().BeTrue(
            "a member id is validated against the spec schema");
    }
}
