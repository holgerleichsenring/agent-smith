using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
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
    private readonly CompletedRunTicketSummary _sut = new(AgentSmith.Tests.TestHelpers.TestRunResultChanges.Create());

    private static IReadOnlyList<RepoConnection> Repos(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new RepoConnection { Name = $"repo-{i}" })];

    [Fact]
    public async Task Build_ListsEveryRepoOutcomeAndEveryChange()
    {
        var opened = new List<OpenedPullRequest>
        {
            new("api", "https://stub.test/pulls/1", OpenStatus.Opened),
            new("worker", null, OpenStatus.SkippedNoChanges),
            new("docs", null, OpenStatus.Failed, "push failed"),
        };
        var changes = new List<CodeChange> { new(new FilePath("src/Guard.cs"), "// guard", "Created") };

        var summary = await _sut.BuildAsync(new PipelineContext(), Repos(1), opened, changes, null, default);

        summary.Should().StartWith("## Agent Smith - Completed across 1 repo(s)")
            .And.Contain("- **api**: https://stub.test/pulls/1")
            .And.Contain("- **worker**: _(no changes)_")
            .And.Contain("- **docs**: _(open failed)_")
            .And.Contain("- [Created] `repo-0/src/Guard.cs`");
    }

    [Fact]
    public async Task Build_AShortfall_LeadsWithItAndNamesWhatIsMissing()
    {
        // p0439: a delivered shortfall is a completed run that says so.
        var shortfall = new Contracts.Specs.RunShortfall(
            [new Contracts.Specs.SpecProgress("p0001a", "Introduce the guard", Contracts.Specs.SpecRunState.Done)],
            [new Contracts.Specs.SpecProgress("p0001b", "Move the callers", Contracts.Specs.SpecRunState.NotStarted)],
            "per-pipeline cost budget exhausted");
        var opened = new List<OpenedPullRequest> { new("api", "https://stub.test/pulls/1", OpenStatus.Opened) };

        var summary = await _sut.BuildAsync(new PipelineContext(), Repos(1), opened, [], shortfall, default);

        summary.Should().StartWith("## Agent Smith - Delivered with a shortfall (1 of 2 specs) across 1 repo(s)")
            .And.Contain("## Not delivered")
            .And.Contain("- **p0001b** — Move the callers (not started)")
            .And.Contain("per-pipeline cost budget exhausted")
            .And.NotContain("Completed across");
    }

    /// <summary>
    /// 2026-11-17-7c9be: the replay listed only plan.md — the last pass's write — while the branch changed 45
    /// files. A code run's comment lists what its branch changed against its base.
    /// </summary>
    [Fact]
    public async Task BuildAsync_ACodeRun_ListsTheBranchNotTheLastToolWrite()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, "code");
        pipeline.Set(ContextKeys.RunId, "2026-11-17T18-36-46-7a0b");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { ["app"] = new GitScriptSandbox("A\tsrc/Guard.cs\nM\tsrc/Api.cs\nA\t.agentsmith/plan.md\n") });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["app"] = "app" });
        var opened = new List<OpenedPullRequest> { new("app", "https://stub.test/pulls/1", OpenStatus.Opened) };

        var summary = await _sut.BuildAsync(pipeline, [new RepoConnection { Name = "app" }], opened,
            [new CodeChange(new FilePath(".agentsmith/plan.md"), "plan", "Create")], null, default);

        summary.Should().Contain("`app/src/Guard.cs`").And.Contain("`app/src/Api.cs`").And.Contain("`app/.agentsmith/plan.md`");
    }
}
