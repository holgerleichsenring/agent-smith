using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0355: partitions a run's repos into changed / unchanged by the sandbox
/// working tree (git status --porcelain), so the post-execute per-repo passes
/// (test/doc generation) never run against a repo the run did not touch —
/// saves compute AND removes the busywork that lets the master drift onto an
/// unchanged repo. Each skip is logged honestly; the caller surfaces it in the
/// step result. A repo with no sandbox partitions as skipped (nothing to run in).
/// </summary>
public sealed class RepoDiffPartitioner(
    SandboxGitOperations gitOps,
    ILogger<RepoDiffPartitioner> logger)
{
    public async Task<RepoDiffPartition> PartitionAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var changedSandboxes = new Dictionary<string, ISandbox>();
        var changedNames = new List<string>();
        var skippedNames = new List<string>();
        foreach (var repo in repos)
        {
            var pairs = SandboxTargets.SandboxesForRepo(pipeline, repo);
            if (await AnyWorkingChangesAsync(pairs, cancellationToken))
            {
                foreach (var (key, sandbox) in pairs) changedSandboxes[key] = sandbox;
                changedNames.Add(repo.Name);
                continue;
            }
            skippedNames.Add(repo.Name);
            logger.LogInformation("No diff in {Repo} — skipped", repo.Name);
        }
        return new RepoDiffPartition(changedSandboxes, changedNames, skippedNames);
    }

    private async Task<bool> AnyWorkingChangesAsync(
        IReadOnlyList<KeyValuePair<string, ISandbox>> pairs, CancellationToken cancellationToken)
    {
        foreach (var (_, sandbox) in pairs)
            if (await gitOps.HasWorkingChangesAsync(sandbox, cancellationToken))
                return true;
        return false;
    }
}
