using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs.Commit;

/// <summary>
/// 2026-11-23-933dc: a work tree holding ONE folder of the source repo — a no-checkout, sparse, blob-less clone of one
/// branch (the configured default branch, or the remote's HEAD when none is configured), the sparse set to that folder
/// alone (no-cone, so not even root files are checked out), then the branch checked out. Returns the branch.
/// </summary>
public sealed class PocSparseCheckout(PocGit git, IGitTokenResolver credentials)
{
    public async Task<string> OpenAsync(ISandbox sandbox, RepoConnection repo, string folder, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        await git.RunStepAsync(sandbox, $"Cloning '{repo.Name}'",
            CheckoutStepFactory.BuildSparseCloneStep(repo, credentials.For(repo), repo.DefaultBranch), ct);
        await git.RunAsync(sandbox, $"Narrowing the work tree to '{folder}'", ["sparse-checkout", "set", "--no-cone", $"/{folder}/"], ct);
        var branch = await git.RunAsync(sandbox, "Reading the cloned branch", ["rev-parse", "--abbrev-ref", "HEAD"], ct);
        await git.RunAsync(sandbox, $"Checking out '{branch}'", ["checkout", branch], ct, repo);
        return branch;
    }
}
