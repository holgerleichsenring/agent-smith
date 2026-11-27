using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sanity;
using Moq;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>2026-11-27-6625: a run holding shop with billing configured beside it, ensure_repo_sandbox as the run
/// answers it, and a file write through the real write guard.</summary>
internal static class SpecRepoCorrectionRig
{
    // No sandbox is staged, so the gate admits the revision.
    public static SpecRevisionRig Rig()
    {
        var rig = new SpecRevisionRig();
        rig.Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [new RepoConnection { Name = "shop" }]);
        rig.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["shop"] = "shop", ["billing"] = "billing" });
        return rig;
    }

    // The repository joins with a sandbox of its own.
    public static Func<string, CancellationToken, Task<string>> Joining(PipelineContext pipeline, List<string> asked) =>
        (repo, _) =>
        {
            asked.Add(repo);
            pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
                [.. pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos), new RepoConnection { Name = repo }]);
            pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
                new Dictionary<string, ISandbox> { [repo] = new Mock<ISandbox>().Object });
            return Task.FromResult($"Sandbox ready for repo '{repo}'");
        };

    public static Task<string> Write(SpecRevisionRig rig, string path) =>
        SanityCalls.Text(rig.Pipeline, "write_file", "Wrote", ("path", path), ("content", "x"));
}
