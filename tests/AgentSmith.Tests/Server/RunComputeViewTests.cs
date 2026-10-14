using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0355: the COMPUTE pod count is the ACTUAL live pods (one per repo, latest row
/// wins) — not every RunSandbox row ever written. A repo whose sandbox was
/// recreated mid-run (a retried ensure_repo_sandbox) left several rows and reported
/// "5 pods" for a 3-repo run; the view now collapses to one pod per repo.
/// </summary>
public sealed class RunComputeViewTests
{
    private static RunSandbox Box(long id, string repo, string? endReason, string mem = "1Gi") =>
        new() { Id = id, RunId = "run-1", Key = repo, RepoName = repo, ToolchainImage = "img", EndReason = endReason, MemoryRequest = mem };

    [Fact]
    public void PodCount_IsActualNotReserved_OneRowPerRepo()
    {
        // 3 repos, but repo "a" has 3 rows (two retries) → 5 rows total.
        var sandboxes = new List<RunSandbox>
        {
            Box(1, "a", "exited"), Box(2, "a", "vanished"), Box(5, "a", null),
            Box(3, "b", null),
            Box(4, "c", null),
        };

        var view = RunComputeView.From(sandboxes);

        view.Should().NotBeNull();
        view!.Pods.Should().HaveCount(3);
        view.Pods.Select(p => p.Repo).Should().BeEquivalentTo("a", "b", "c");
        // The LATEST row for "a" (id 5, still open) wins over the stale retries.
        view.Pods.Single(p => p.Repo == "a").EndReason.Should().BeNull();
    }

    [Fact]
    public void NoSandboxes_IsNull_ClientShowsCalculating()
    {
        RunComputeView.From(new List<RunSandbox>()).Should().BeNull();
    }

    [Fact]
    public void TotalMem_SumsOneRowPerRepo()
    {
        var sandboxes = new List<RunSandbox>
        {
            Box(1, "a", "exited", "2Gi"), Box(2, "a", null, "2Gi"),
            Box(3, "b", null, "2Gi"),
        };

        // 2 live pods x 2Gi = 4Gi (the stale retry row for "a" is NOT double-counted).
        RunComputeView.From(sandboxes)!.TotalMem.Should().Be("4Gi");
    }

    [Fact]
    public void RunComputeView_PodCarriesEndReason()
    {
        var box = Box(1, "a", "oom-killed");
        box.ExitCode = 137;
        box.OomKilled = true;

        var pod = RunComputeView.From(new List<RunSandbox> { box })!.Pods.Single();

        pod.EndReason.Should().Be("oom-killed");
        pod.ExitCode.Should().Be(137);
        pod.OomKilled.Should().BeTrue();
    }
}
