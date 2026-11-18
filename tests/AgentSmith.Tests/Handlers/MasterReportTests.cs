using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>2026-11-18-c8e2b: the master's report in its verdict — a line said not met with why, what it fixed in
/// passing, the follow-ups it proposes — and what its own acceptance gate makes of a reasoned not-met.</summary>
public sealed class MasterReportTests
{
    private const string Report = """
        ```verdict
        { "status": "green", "build_ran": true, "build_passed": true, "tests_ran": true, "tests_passed": true,
          "acceptance": [
            { "criterion": "GET /orders/{id} returns 404 for an unknown id", "status": "met", "evidence": "OrdersController.cs" },
            { "criterion": "The export endpoint streams CSV", "status": "not_met", "evidence": "needs a paging contract first; proposed as a follow-up" } ],
          "fixed_in_passing": ["a null check the touched handler lacked"],
          "follow_ups": [ { "goal": "The export endpoint streams CSV over a paging contract", "why": "the paging contract is its own design" },
                          "Rename the legacy order DTO" ],
          "summary": "1 of 2 met; one follow-up" }
        ```
        """;

    private static readonly IReadOnlyList<string> Criteria =
        ["GET /orders/{id} returns 404 for an unknown id", "The export endpoint streams CSV"];

    [Fact]
    public void TryParse_AReport_CarriesNotMetFixedInPassingAndFollowUps()
    {
        var verdict = MasterVerificationParser.TryParse(Report)!;

        verdict.AcceptanceDispositions![1].Status.Should().Be(AcceptanceStatus.NotMet);
        verdict.FixedInPassing.Should().Equal("a null check the touched handler lacked");
        verdict.FollowUps.Should().Equal(
            new MasterFollowUp("The export endpoint streams CSV over a paging contract", "the paging contract is its own design"),
            new MasterFollowUp("Rename the legacy order DTO", ""));
    }

    [Fact]
    public void TryParse_AVerdictWithoutTheReport_LeavesItNull()
    {
        var verdict = MasterVerificationParser.TryParse("""{ "status": "green", "acceptance": [ { "criterion": "c", "status": "unmet", "evidence": "open" } ] }""")!;

        verdict.FixedInPassing.Should().BeNull();
        verdict.FollowUps.Should().BeNull();
        verdict.AcceptanceDispositions!.Single().Status.Should().Be(AcceptanceStatus.Unmet, "'unmet' stays the fail-closed default");
    }

    [Fact]
    public void Judge_AReasonedNotMet_IsAnsweredButNotSatisfied()
    {
        var judgement = MasterAcceptanceGate.Judge(MasterVerificationParser.TryParse(Report), Criteria, producedSourceChanges: true);

        judgement.Satisfied.Should().BeTrue("the master's call owes no re-drive");
        judgement.Pairings.Count(p => p.Satisfied).Should().Be(1, "a not-met line still counts as not met");
        judgement.Describe().Should().Contain("not met (the master's call)");
    }

    [Theory]
    [InlineData("not_met", "")]
    [InlineData("partial", "half of it")]
    [InlineData("unmet", "still open")]
    public void Judge_ANotMetWithoutWhy_OrAnUnknownStatus_IsNotAnswered(string status, string evidence)
    {
        var verdict = new MasterVerification(VerificationStatus.Green, true, true, true, true, "s",
            AcceptanceDispositions:
            [
                new AcceptanceDisposition(Criteria[0], AcceptanceStatus.Met, "an edit"),
                new AcceptanceDisposition(Criteria[1], MasterVerificationParser.TryParse(
                    $$"""{ "status": "green", "acceptance": [ { "criterion": "x", "status": "{{status}}" } ] }""")!
                    .AcceptanceDispositions!.Single().Status, evidence),
            ]);

        MasterAcceptanceGate.ObjectivelySatisfied(verdict, Criteria, producedSourceChanges: true).Should().BeFalse();
    }
}
