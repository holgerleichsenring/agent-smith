using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: the rung a HELD source scope takes instead of a clone. A sandbox kept
/// between the turns of a design conversation already carries its repository, and git refuses
/// to clone into a non-empty directory with a message the classifier reads as an unreachable
/// host — so the work path is read first, and a tree already there is brought to the remote's
/// own HEAD rather than fetched again from nothing.
/// <para>
/// Nothing here trusts the register: the tree is asked who it is a clone OF, so a hold handed
/// over under the wrong key cannot make one conversation read another's repository.
/// </para>
/// <para>
/// It is a service rather than a static helper because it takes the sandbox it acts on:
/// a static that needs a collaborator is a service without a constructor.
/// </para>
/// </summary>
public sealed class SourceScopeRefresh
{
    /// <summary>
    /// Whether this work path is a clone of THIS repository — asked of the tree itself, never
    /// taken from whoever handed the sandbox over. A non-zero exit or another remote is a
    /// work path that must be cloned into.
    /// </summary>
    public async Task<bool> HoldsRepoAsync(
        ISandbox sandbox, RepoConnection repo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(repo.Url)) return false;
        var origin = await sandbox.RunStepAsync(
            SourceScopeRefreshSteps.BuildRemoteUrlStep(), null, ct);
        return origin.ExitCode == 0 && string.Equals(
            (origin.OutputContent ?? string.Empty).Trim(), repo.Url, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The tree already there, made identical to what the remote calls HEAD now. The fetch is
    /// the rung that talks to the remote, so its failure is classified as the clone's is; a
    /// reset that fails over what the fetch delivered could not reach the remote's state either.
    /// </summary>
    public async Task ToRemoteHeadAsync(ISandbox sandbox, RepoConnection repo, CancellationToken ct)
    {
        var fetch = await sandbox.RunStepAsync(
            SourceScopeRefreshSteps.BuildFetchHeadAtDepthStep(repo), null, ct);
        if (fetch.ExitCode != 0)
            throw SourceScopeFailures.Fail(SourceScopeFailures.KindOf(fetch), repo, revision: null,
                $"the held tree could not be refreshed: {SourceScopeFailures.Text(fetch)}");

        var reset = await sandbox.RunStepAsync(
            SourceScopeRefreshSteps.BuildResetHardStep("FETCH_HEAD"), null, ct);
        if (reset.ExitCode != 0)
            throw SourceScopeFailures.Fail(SourceScopeFailureKind.Unreachable, repo, revision: null,
                $"the held tree would not reset onto what the remote sent: {SourceScopeFailures.Text(reset)}");
    }
}
