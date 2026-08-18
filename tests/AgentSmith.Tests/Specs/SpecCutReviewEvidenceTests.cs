using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0446: the reviewer's three verdicts do not need the same evidence.
/// <para>
/// A CONTRADICTION is visible in the phases alone, and so is an UNCHECKABLE criterion.
/// NOT IN THE TICKET is the only one that needs the ticket, and it needs ALL of it —
/// "the ticket never asked for this" is not a claim a fragment can support.
/// </para>
/// <para>
/// Live run 2552 is what this costs. A 44,302-character ticket was cut to the first
/// 20,000 before the reviewer saw it, and it then rejected five criteria of phase d as
/// not-in-ticket. All five are in the ticket — `dotnet test` with "All tests must be
/// green" at character 39,840, the eight-point final-report list at 42,500, `ASB-6` at
/// 30,373. The deriver spent its three attempts trying to satisfy an objection that
/// could not be satisfied, gave up, and the run carried the whole ticket in one phase.
/// </para>
/// </summary>
public sealed class SpecCutReviewEvidenceTests
{
    [Fact]
    public void AWholeTicket_LetsTheReviewerSayACriterionWasNeverAskedFor()
    {
        var prompt = SpecCutReviewPrompt.For(Cut(), "migrate the messaging library");

        prompt.Should().Contain("NOT IN THE TICKET");
    }

    [Fact]
    public void ATicketTooLongToShowWhole_WithholdsThatVerdict()
    {
        var prompt = SpecCutReviewPrompt.For(Cut(), new string('x', 400_000));

        prompt.Should().NotContain("NOT IN THE TICKET",
            "a fragment cannot support the claim that the ticket never asked for something");
        prompt.Should().Contain("CONTRADICTION", "the other two verdicts still hold");
    }

    /// <summary>
    /// The ticket this was found on. A migration manual of this size is the normal case,
    /// not an outlier, and it has to reach the reviewer whole.
    /// </summary>
    [Fact]
    public void ATicketOfTheSizeThisWasFoundOn_ReachesTheReviewerWhole()
    {
        var ticket = new string('y', 44_302) + "THE-LAST-REQUIREMENT";

        var prompt = SpecCutReviewPrompt.For(Cut(), ticket);

        prompt.Should().Contain("THE-LAST-REQUIREMENT");
        prompt.Should().Contain("NOT IN THE TICKET");
    }

    private static SpecSet Cut() =>
        new("azuredevops-1",
            [new SpecPhase(
                new PhaseDraft("p1a", "migrate the senders", "phase: p1a", [])
                {
                    Done = ["every sender uses the new bus"],
                },
                "migrate-the-senders", "# p1a", [])],
            SpecAccounting.Empty, [], SpecSource.Derived);
}
