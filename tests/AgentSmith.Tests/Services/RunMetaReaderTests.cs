using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class RunMetaReaderTests : IDisposable
{
    private readonly string _root;

    public RunMetaReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "agent-smith-runs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void RunMetaReader_PreP0169aRun_ReturnsUnknownPlaceholders()
    {
        const string runId = "2026-04-09T10-00-00-aaaa";
        var dir = Path.Combine(_root, runId + "-old-run");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "result.md"),
            "---\nticket: \"#42 — old\"\ndate: 2026-04-09\nresult: success\ntype: feat\n---\n# header\n");

        var meta = NewReader().Read(runId);

        meta.Should().NotBeNull();
        meta!.PipelineName.Should().BeNull();
        meta.RepoMode.Should().Be("unknown");
        meta.SandboxCount.Should().Be(0);
        meta.Repos.Should().BeEmpty();
        meta.Status.Should().Be("success"); // legacy `result: success` falls back into Status
    }

    [Fact]
    public void RunMetaReader_NewRun_ParsesTopologyFields()
    {
        const string runId = "2026-05-20T22-27-43-8a3f";
        var dir = Path.Combine(_root, runId + "-add-feature");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "result.md"),
            "---\n" +
            "run_id: " + runId + "\n" +
            "pipeline_name: fix-bug\n" +
            "status: done\n" +
            "started_at: 2026-05-20T22:27:43Z\n" +
            "duration_seconds: 120\n" +
            "repo_mode: multi\n" +
            "sandbox_count: 3\n" +
            "repos:\n  - api\n  - web\n  - worker\n" +
            "---\n");

        var meta = NewReader().Read(runId);

        meta.Should().NotBeNull();
        meta!.PipelineName.Should().Be("fix-bug");
        meta.Status.Should().Be("done");
        meta.RepoMode.Should().Be("multi");
        meta.SandboxCount.Should().Be(3);
        meta.DurationSeconds.Should().Be(120);
        meta.Repos.Should().BeEquivalentTo(new[] { "api", "web", "worker" });
        meta.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void RunMetaReader_ListAll_NewestFirst()
    {
        WriteRun("2026-05-01T00-00-00-aaaa", "first");
        WriteRun("2026-05-20T22-27-43-8a3f", "second");
        WriteRun("2026-05-10T10-00-00-bbbb", "middle");

        var all = NewReader().ListAll();

        all.Should().HaveCount(3);
        all[0].RunId.Should().Be("2026-05-20T22-27-43-8a3f");
        all[2].RunId.Should().Be("2026-05-01T00-00-00-aaaa");
    }

    private void WriteRun(string runId, string slug)
    {
        var dir = Path.Combine(_root, $"{runId}-{slug}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "result.md"),
            $"---\nrun_id: {runId}\npipeline_name: fix-bug\nstatus: done\n---\n");
    }

    private RunMetaReader NewReader() => new(new TestRootResolver(_root));

    private sealed class TestRootResolver(string root) : IRunsRootResolver
    {
        public string Resolve() => root;
    }
}
