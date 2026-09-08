using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-09-07-f420: the completed-run summary, extracted from CommitAndPRHandler so the
/// wording of a completed run has exactly one author.
/// </summary>
public sealed class CompletedRunTicketSummaryTests
{
    private readonly CompletedRunTicketSummary _sut = new();

    [Fact]
    public void Build_ListsEveryRepoOutcomeAndEveryChange()
    {
        var opened = new List<OpenedPullRequest>
        {
            new("api", "https://stub.test/pulls/1", OpenStatus.Opened),
            new("worker", null, OpenStatus.SkippedNoChanges),
            new("docs", null, OpenStatus.Failed, "push failed"),
        };
        var changes = new List<CodeChange> { new(new FilePath("src/Guard.cs"), "// guard", "Created") };

        var summary = _sut.Build(new PipelineContext(), 3, opened, changes);

        summary.Should().StartWith("## Agent Smith - Completed across 3 repo(s)")
            .And.Contain("- **api**: https://stub.test/pulls/1")
            .And.Contain("- **worker**: _(no changes)_")
            .And.Contain("- **docs**: _(open failed)_")
            .And.Contain("- [Created] `src/Guard.cs`");
    }

    /// <summary>2026-09-06-3d81: the ticket author reads which sentence of their ticket no
    /// work could satisfy, and the master's evaluated reason for it.</summary>
    [Fact]
    public void Outcome_ADeclinedCriterion_IsRenderedInTheCompletionTicketComment()
    {
        var pipeline = new PipelineContext();
        DeclinedCriteriaLedger.Record(pipeline, new MasterVerification(
            VerificationStatus.Green, true, true, true, true, "done",
            AcceptanceDispositions: [new AcceptanceDisposition(
                "`npm run lint` exits 0", AcceptanceStatus.NotApplicable,
                "the lint config is a shared package this repository cannot change")]));

        var summary = _sut.Build(pipeline, 1, [new("api", "https://stub.test/pulls/1", OpenStatus.Opened)], []);

        summary.Should().Contain(DeclinedCriteriaSection.Heading)
            .And.Contain("**`npm run lint` exits 0** — the lint config is a shared package this repository cannot change")
            .And.EndWith("This ticket was automatically processed by Agent Smith.");
    }
}
