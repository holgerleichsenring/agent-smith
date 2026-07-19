using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: the single "get this repo's source into these sandboxes" path —
/// resolve the branch via ISourceProvider, then `git clone` + branch-switch
/// inside each sandbox at /work. Extracted from CheckoutSourceHandler so the
/// mid-run ensure_repo_sandbox escalation reuses the exact checkout the
/// pipeline's CheckoutSource step performs (no second implementation to drift).
/// Local providers trust the bind-mount and skip the clone.
/// </summary>
public sealed class SandboxRepoCloner(
    ISourceProviderFactory factory,
    ILogger<SandboxRepoCloner> logger)
{
    /// <summary>Returns the checked-out Repository, or null on failure (logged).</summary>
    public async Task<Repository?> CheckoutIntoSandboxesAsync(
        RepoConnection config, BranchName? branch,
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        var provider = factory.Create(config);
        var resolved = await provider.CheckoutAsync(branch, ct);
        var repo = new Repository(resolved.CurrentBranch, resolved.RemoteUrl);

        if (provider.ProviderType.Equals("Local", StringComparison.OrdinalIgnoreCase))
            return repo;

        if (sandboxes.Count == 0)
            return FailWith($"No sandbox for repo '{config.Name}'.", config);
        if (string.IsNullOrEmpty(config.Url))
            return FailWith("Checkout requires a non-empty source URL for non-local providers.", config);

        foreach (var (key, sandbox) in sandboxes)
        {
            var clone = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCloneStep(config), null, ct);
            if (clone.ExitCode != 0)
                return FailWith(
                    $"git clone into sandbox '{key}' failed (exit={clone.ExitCode}): {clone.ErrorMessage}", config);
            await MaybeSwitchBranchAsync(sandbox, branch, ct);
        }
        return repo;
    }

    private Repository? FailWith(string message, RepoConnection config)
    {
        logger.LogWarning("{Repo}: {Message}", config.Name, message);
        return null;
    }

    // Check out the ticket branch's EXISTING content on a re-run (build on the
    // prior run's committed work), falling back to creating it from the clone's
    // default HEAD on a first run. The full `git clone` fetches every branch, so
    // `git checkout <ticket-branch>` finds origin/<branch> and lands its content.
    // No early-out on "requested == resolved": CheckoutAsync only ECHOES the
    // requested branch back as CurrentBranch, so that guard was always true and
    // silently skipped the switch — the sandbox stayed on the default and the
    // prior run's work was ignored (then force-pushed over). `git checkout` on the
    // branch we are already on is a harmless no-op, so the switch is unconditional.
    private async Task MaybeSwitchBranchAsync(
        ISandbox sandbox, BranchName? requested, CancellationToken ct)
    {
        if (requested is null)
            return;
        var branch = requested.Value;

        var existing = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(branch), null, ct);
        if (existing.ExitCode == 0)
        {
            logger.LogInformation("git checkout {Branch} (existing)", branch);
            return;
        }
        var created = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCreateBranchStep(branch), null, ct);
        if (created.ExitCode != 0)
            logger.LogWarning(
                "git checkout -b {Branch} failed (exit={Exit}): {Err}",
                branch, created.ExitCode, created.ErrorMessage);
    }
}
