using AgentSmith.Infrastructure.Services.Providers.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5a: what a partly-typed ticket number stands for.
/// <para>
/// An operator typed "194" and a tracker's own box answered five work items, four of them by ID
/// PREFIX — while this framework answered one, work item 194 itself. There is no LIKE in WIQL and
/// its substring operator is not defined on an integer, so a prefix is the ids it covers: as
/// ranges where a language compares, as an enumeration where it only names.
/// </para>
/// </summary>
public sealed class TicketNumberPrefixTests
{
    [Fact]
    public void TicketNumberPrefix_ThreeDigits_BecomesTheIdsItStandsFor()
    {
        TicketNumberPrefix.Ranges("194").Should().Equal(
            (194L, 194L), (1940L, 1949L), (19400L, 19499L));
    }

    [Fact]
    public void TicketNumberPrefix_TheSameDepthEverywhere_KeepsEnumerationAffordable()
    {
        // Ranges scale and enumeration does not, so both are held to two extra digits: three
        // disjuncts on one tracker, a hundred and eleven named ids on the others.
        TicketNumberPrefix.Ids("194").Should().HaveCount(111);
        TicketNumberPrefix.Ids("194").Should().Contain(19400).And.NotContain(194000);
    }

    [Fact]
    public void TicketNumberPrefix_TextThatIsNotAPlainNumber_IsNoPrefix()
    {
        TicketNumberPrefix.Of("DPG-19").Should().BeNull();
        TicketNumberPrefix.Of("cannot log in").Should().BeNull();
        TicketNumberPrefix.Of(" 194 ").Should().Be("194", "a typed number may be padded");
    }

    [Fact]
    public void TicketNumberPrefix_AzureDevOps_ComparesRatherThanMatchingText()
    {
        var clause = AzureDevOpsIdPrefixClause.For("194");

        clause.Should().Be(
            "([System.Id] = 194 OR ([System.Id] >= 1940 AND [System.Id] <= 1949) "
            + "OR ([System.Id] >= 19400 AND [System.Id] <= 19499))");
        // The tree's own discovery builder records why: WIQL refuses a quoted value against an
        // integer field, so CONTAINS is not available here at all.
        clause.Should().NotContain("CONTAINS");
    }
}
