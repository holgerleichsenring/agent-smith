using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0331: the single "get this repo's source into these sandboxes" path — resolve the branch via ISourceProvider,
/// then `git clone` + branch-switch inside each sandbox at its root (/work/&lt;repo&gt;); CheckoutSource and the
/// mid-run ensure_repo_sandbox share it. Local providers trust the bind-mount and skip the clone. p0411: checkout
/// is also where the sandbox's committing git identity is established, once.
/// </summary>
public sealed class SandboxRepoCloner(
    ISourceProviderFactory factory,
    IGitTokenResolver credentials,
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
            // 2026-11-17-d5cc: a bind mount is trusted as it is; an empty sandbox is cloned like a remote repo's.
            if (await LocalRepoCheckout.IsBindMountedAsync(sandboxes, ct))
            {
                await EnsureIdentityAsync(sandboxes, ct);
                return RepoCheckout.Ready(repo);
            }
            if (LocalRepoCheckout.CloneSource(config) is not { } source)
                return FailWith(LocalRepoCheckout.NothingToClone, config);
            config = source;
        }

        if (sandboxes.Count == 0)
            return FailWith($"No sandbox for repo '{config.Name}'.", config);
        if (string.IsNullOrEmpty(config.Url))
            return FailWith("Checkout requires a non-empty source URL for non-local providers.", config);

        // 2026-10-02-5f89g: the repo's own credential, resolved once; a missing secret refuses the
        // checkout by name instead of leaving git to prompt nobody.
        var (credential, missing) = Credential(config);
        if (credential is null) return FailWith(missing!, config);

        // 2026-11-25-9992b: every key of a repository is a toolchain container over the SAME tree of the run's
        // workspace, so the repository is cloned and placed once, through its first key.
        var (key, sandbox) = sandboxes[0];
        var clone = await CloneIntoRootAsync(sandbox, config, credential, ct);
        if (clone.ExitCode != 0) return FailWith(CloneProblem(key, clone), config);
        // p0496: the identity comes BEFORE the branch switch — a three-way base merge commits.
        await identity.EnsureConfiguredAsync(sandbox, config.Machine, ct); // 2026-11-25-9992h: the machine commits
        var placement = await branchCheckout.SwitchAsync(sandbox, config, branch, ct);
        return placement.Problem is not null
            ? FailWith($"sandbox '{key}': {placement.Problem}", config)
            : RepoCheckout.Ready(repo, placement.Rung);
    }

    // 2026-11-25-9992c: the clone lands IN the root, made first when it is not the workspace root.
    private static async Task<StepResult> CloneIntoRootAsync(
        ISandbox sandbox, RepoConnection config, GitCredential credential, CancellationToken ct)
    {
        var root = sandbox.WorkDirectory();
        if (CheckoutStepFactory.BuildMakeRootStep(root) is { } makeRoot
            && await sandbox.RunStepAsync(makeRoot, null, ct) is { ExitCode: not 0 } refused)
            return refused;
        return await sandbox.RunStepAsync(CheckoutStepFactory.BuildCloneStep(config, credential, root), null, ct);
    }

    // A fresh clone has no committing identity; a bind-mounted repo's configured one is left untouched.
    private async Task EnsureIdentityAsync(
        IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        foreach (var (_, sandbox) in sandboxes)
            await identity.EnsureConfiguredAsync(sandbox, ct);
    }

    // 2026-08-25-014d: an image that carries no git is discovered here and named, never read as a broken repository.
    private static string CloneProblem(string key, StepResult clone) =>
        MissingGitInImage.Explains(clone)
            ? $"git clone into sandbox '{key}' could not start: {MissingGitInImage.Cause} "
              + $"(exit={clone.ExitCode}: {clone.ErrorMessage})"
            : $"git clone into sandbox '{key}' failed (exit={clone.ExitCode}): {clone.ErrorMessage}";

    private (GitCredential? Credential, string? Problem) Credential(RepoConnection config)
    {
        try
        {
            return (credentials.For(config), null);
        }
        catch (MissingCredentialException ex)
        {
            logger.LogDebug(ex, "{Repo}: no credential behind its auth secret", config.Name);
            return (null, ex.Message);
        }
    }

    private RepoCheckout FailWith(string message, RepoConnection config)
    {
        logger.LogWarning("{Repo}: {Message}", config.Name, message);
        return RepoCheckout.Failed(message);
    }
}
