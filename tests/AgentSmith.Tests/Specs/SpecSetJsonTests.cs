using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-28-db88a: the TicketSeries row stores a ticket's set as SpecSetJson, and a run takes it from there — so every
/// field a run reads must come back exactly: the hand-back with its readings and question kind, the contract, the
/// stand-ins, the series repositories, the goal, the approval, the accounting, the revisions, the executed ids, the mocks.
/// </summary>
public sealed class SpecSetJsonTests
{
    [Fact]
    public void Read_ASetWithEveryFieldPopulated_RoundTripsUnchanged()
    {
        var set = Full();

        var read = SpecSetJson.Read(SpecSetJson.Write(set));

        read.Should().NotBeNull();
        SpecSetJson.Write(read!).Should().Be(SpecSetJson.Write(set));
        read.Should().BeEquivalentTo(set);
        read.Executed.Should().Equal("2026-11-28-0a0aa");
        read.Handback!.QuestionKind.Should().Be(SpecQuestionKind.ReviewObjection);
        read.Handback.TakenReading.Should().Be("the second reading");
        read.Contract!.Source.Should().Be("series-contract.yaml");
        read.Specs[1].Draft.RatifiedMock.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"key\":\"jira-1\"}")]
    public void Read_AbsentUnreadableOrHollow_IsNoSet(string? json)
    {
        SpecSetJson.Read(json).Should().BeNull();
    }

    private static SpecSet Full()
    {
        var first = new DerivedSpec(
            new SpecDraft("2026-11-28-0a0aa", "Introduce the guard", "spec: 2026-11-28-0a0aa\ngoal: \"Introduce the guard\"\n", [])
            {
                Done = ["An empty body is answered with 400."], Steps = [new SpecStep("guard", "Add the guard", "src/Guard.cs")],
                Tests = ["Guard_EmptyBody_Is400"], Facts = [new SpecFact("The API has no guard", "src/Api.cs:12")],
                Assumptions = ["Bodies are JSON"], Contexts = ["api"], Repos = ["Server"],
            },
            "introduce-the-guard", "## Companion\nThe ticket span.", [1, 2], [".agentsmith/specs/planned/2026-11-28-0a0aa.mock.html"]);
        var second = new DerivedSpec(
            new SpecDraft("2026-11-28-0a0ab", "Move the callers", "spec: 2026-11-28-0a0ab\n", ["2026-11-28-0a0aa"])
            {
                Done = ["No caller checks on its own."], Repos = ["Client"], RatifiedMock = 2, Supersedes = ["2026-11-27-0b0ba"],
            },
            "move-the-callers", string.Empty, [3]);
        var accounting = new SpecAccounting(
            [new CarriedSegment(1, "2026-11-28-0a0aa"), new CarriedSegment(3, "2026-11-28-0a0ab")],
            [new DiscardedSegment(4, "out of scope")], [5], [new DiscardedContext("docs", "no change")]) { Shared = [2] };
        var handback = new SpecHandback(SpecHandbackCase.Question, "Which reading?", "the quoted sentence",
            ["the first reading", "the second reading"], 1)
        {
            Cites = ["E1: src/Api.cs:12"], QuestionKind = SpecQuestionKind.ReviewObjection,
        };
        var contract = new SeriesContract(
            [new SeriesContractName("Guard", "class", 1, "2026-11-28-0a0aa", ["2026-11-28-0a0ab"])],
            [new SeriesContractDatum("payload", "Api.Request", "2026-11-28-0a0aa")],
            [new SeriesContractArea("2026-11-28-0a0ab", ["src/Callers/"])],
            [new SeriesContractBudget("src/Guard.cs", "2026-11-28-0a0aa", 80)]) { Source = "series-contract.yaml" };
        return new SpecSet("jira-1", [first, second], accounting,
            [new SpecRevision(1, "approved in design conversation c-1", new DateTimeOffset(2026, 11, 28, 9, 0, 0, TimeSpan.Zero)),
             new SpecRevision(2, "re-trigger on the ticket", new DateTimeOffset(2026, 11, 28, 10, 30, 15, 250, TimeSpan.FromHours(1)))],
            SpecSource.BranchArtifact, handback, TicketPinnedWhole: true, ExecutedSpecIds: ["2026-11-28-0a0aa"],
            TicketFingerprint: "fp-1", Approval: new SpecApproval(new DateTimeOffset(2026, 11, 28, 8, 0, 0, TimeSpan.Zero), "c-1", "sample.approver"),
            Series: "2026-11-28-0a0a")
        {
            Goal = "Guard every payload", SeriesRepos = ["Server", "Client"], Contract = contract, StandIns = ["2026-11-28-0a0ab"],
        };
    }
}
