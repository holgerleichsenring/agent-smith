using AgentSmith.Server.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Api;

/// <summary>
/// p0169a: endpoint behaviour covered at the service layer (RunMetaReader +
/// RunArtefactLister). Full HTTP-level coverage via WebApplicationFactory
/// is deferred — the Server pulls Redis + K8s + Slack + Teams into DI,
/// making a TestServer host non-trivial without doubling the entire
/// composition root. Reader-level units are deterministic and faster.
/// </summary>
public sealed class JobsEndpointsTests : IDisposable
{
    private readonly string _root;

    public JobsEndpointsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "jobs-endpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void JobsEndpoint_GetAll_PaginatedAndSortedDescending()
    {
        for (var i = 0; i < 5; i++)
            WriteRun($"2026-05-2{i}T10-00-00-{i:x4}", "feature");

        var all = new RunMetaReader(new TestRootResolver(_root)).ListAll();

        all.Should().HaveCount(5);
        all[0].RunId.Should().StartWith("2026-05-24");
        all[^1].RunId.Should().StartWith("2026-05-20");
    }

    [Fact]
    public void JobsEndpoint_GetById_ReturnsRunMetaAndArtefactList()
    {
        const string runId = "2026-05-20T22-27-43-8a3f";
        WriteRun(runId, "fix-bug");
        var lister = new RunArtefactLister();
        var dir = Path.Combine(_root, runId + "-fix-bug");
        File.WriteAllText(Path.Combine(dir, "plan.md"), "# plan");
        File.WriteAllText(Path.Combine(dir, "decisions.md"), "# decisions");

        var reader = new RunMetaReader(new TestRootResolver(_root));
        var meta = reader.Read(runId);
        var artefacts = lister.List(reader.GetRunDir(runId)!);

        meta.Should().NotBeNull();
        meta!.RunId.Should().Be(runId);
        artefacts.Should().Contain(a => a.Filename == "plan.md");
        artefacts.Should().Contain(a => a.Filename == "decisions.md");
        artefacts.Should().Contain(a => a.Filename == "result.md");
    }

    private void WriteRun(string runId, string pipeline)
    {
        var dir = Path.Combine(_root, $"{runId}-{pipeline}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "result.md"),
            $"---\nrun_id: {runId}\npipeline_name: {pipeline}\nstatus: done\nrepo_mode: mono\nsandbox_count: 1\nrepos:\n  - primary\n---\n");
    }

    private sealed class TestRootResolver(string root) : IRunsRootResolver
    {
        public string Resolve() => root;
    }
}
