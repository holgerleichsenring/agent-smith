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
    private static RunSandbox Box(long id, string repo, string status, string mem = "1Gi") =>
        new() { Id = id, RunId = "run-1", Key = repo, RepoName = repo, ToolchainImage = "img", Status = status, MemoryRequest = mem };

    [Fact]
    public void PodCount_IsActualNotReserved_OneRowPerRepo()
    {
        // 3 repos, but repo "a" has 3 rows (two retries) → 5 rows total.
        var sandboxes = new List<RunSandbox>
        {
            Box(1, "a", "failed"), Box(2, "a", "vanished"), Box(5, "a", "created"),
            Box(3, "b", "created"),
            Box(4, "c", "created"),
        };

        var view = RunComputeView.From(sandboxes);

        view.Should().NotBeNull();
        view!.Pods.Should().HaveCount(3);
        view.Pods.Select(p => p.Repo).Should().BeEquivalentTo("a", "b", "c");
        // The LATEST row for "a" (id 5, "created") wins over the stale retries.
        view.Pods.Single(p => p.Repo == "a").Status.Should().Be("created");
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
            Box(1, "a", "failed", "2Gi"), Box(2, "a", "created", "2Gi"),
            Box(3, "b", "created", "2Gi"),
        };

        // 2 live pods x 2Gi = 4Gi (the stale retry row for "a" is NOT double-counted).
        RunComputeView.From(sandboxes)!.TotalMem.Should().Be("4Gi");
    }
}
