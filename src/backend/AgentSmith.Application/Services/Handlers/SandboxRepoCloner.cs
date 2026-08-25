using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: the single "get this repo's source into these sandboxes" path —
/// resolve the branch via ISourceProvider, then `git clone` + branch-switch
/// inside each sandbox at /work. Extracted from CheckoutSourceHandler so the
/// mid-run ensure_repo_sandbox escalation reuses the exact checkout the
/// pipeline's CheckoutSource step performs (no second implementation to drift).
/// Local providers trust the bind-mount and skip the clone.
/// p0411: checkout is also where the sandbox's committing git identity is
/// established — once, so the staging/commit/checkpoint paths stop re-writing a
/// fact that has not changed since the repo arrived.
/// </summary>
public sealed class SandboxRepoCloner(
    ISourceProviderFactory factory,
    SandboxGitIdentity identity,
    SandboxWorkBranchCheckout branchCheckout,
    ILogger<SandboxRepoCloner> logger)
{
    /// <summary>Returns the checked-out Repository, or the reason it cannot be used.</summary>
    public async Task<RepoCheckout> CheckoutIntoSandboxesAsync(
        RepoConnection config, RunBranch? branch,
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        var provider = factory.Create(config);
        var resolved = await provider.CheckoutAsync(branch?.Name, ct);
        var repo = new Repository(resolved.CurrentBranch, resolved.RemoteUrl);

        if (provider.ProviderType.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureIdentityAsync(sandboxes, ct);
            return RepoCheckout.Ready(repo);
        }

        if (sandboxes.Count == 0)
            return FailWith($"No sandbox for repo '{config.Name}'.", config);
        if (string.IsNullOrEmpty(config.Url))
            return FailWith("Checkout requires a non-empty source URL for non-local providers.", config);

        foreach (var (key, sandbox) in sandboxes)
        {
            var clone = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCloneStep(config), null, ct);
            if (clone.ExitCode != 0) return FailWith(CloneProblem(key, clone), config);
            // p0496: the identity comes BEFORE the branch switch. A base merge writes a
            // merge commit, and a sandbox with no committing user cannot make one — a
            // fast-forward passed without it and a three-way merge did not.
            await identity.EnsureConfiguredAsync(sandbox, ct);
            var problem = await branchCheckout.SwitchAsync(sandbox, branch, ct);
            if (problem is not null) return FailWith($"sandbox '{key}': {problem}", config);
        }
        return RepoCheckout.Ready(repo);
    }

    // A fresh clone has no committing identity, so every path that produces a
    // working sandbox ends here. The bind-mounted local repo goes through it too —
    // the probe leaves an identity the operator already configured untouched.
    private async Task EnsureIdentityAsync(
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        foreach (var (_, sandbox) in sandboxes)
            await identity.EnsureConfiguredAsync(sandbox, ct);
    }

    // 2026-08-25-014d: no part of the product judges an image by its name any more, so
    // the image that turns out to carry no git is discovered right here — and says so,
    // instead of leaving an operator to read `exit=-1` as a broken repository.
    private static string CloneProblem(string key, StepResult clone) =>
        MissingGitInImage.Explains(clone)
            ? $"git clone into sandbox '{key}' could not start: {MissingGitInImage.Cause} "
              + $"(exit={clone.ExitCode}: {clone.ErrorMessage})"
            : $"git clone into sandbox '{key}' failed (exit={clone.ExitCode}): {clone.ErrorMessage}";

    private RepoCheckout FailWith(string message, RepoConnection config)
    {
        logger.LogWarning("{Repo}: {Message}", config.Name, message);
        return RepoCheckout.Failed(message);
    }
}
