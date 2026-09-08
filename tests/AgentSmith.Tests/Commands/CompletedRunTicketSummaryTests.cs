using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
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

    [Fact]
    public void Build_AShortfall_LeadsWithItAndNamesWhatIsMissing()
    {
        // p0439: a delivered shortfall is a completed run that says so.
        var shortfall = new Contracts.Specs.RunShortfall(
            [new Contracts.Specs.PhaseProgress("p0001a", "Introduce the guard", Contracts.Specs.PhaseRunState.Done)],
            [new Contracts.Specs.PhaseProgress("p0001b", "Move the callers", Contracts.Specs.PhaseRunState.NotStarted)],
            "per-pipeline cost budget exhausted");
        var opened = new List<OpenedPullRequest> { new("api", "https://stub.test/pulls/1", OpenStatus.Opened) };

        var summary = _sut.Build(new PipelineContext(), 1, opened, [], shortfall);

        summary.Should().StartWith("## Agent Smith - Delivered with a shortfall (1 of 2 phases) across 1 repo(s)")
            .And.Contain("## Not delivered")
            .And.Contain("- **p0001b** — Move the callers (not started)")
            .And.Contain("per-pipeline cost budget exhausted")
            .And.NotContain("Completed across");
    }
}
