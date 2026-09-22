using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-9802: puts a read-only sandbox onto the revision it was asked for, and says
/// precisely why when it cannot. The refusals it tells apart live in
/// <see cref="SourceScopeFailures"/>.
/// <para>
/// It lives outside <see cref="SourceScopeSandbox"/> because that class owns a different
/// job — the read-only guard and the lazy spawn — and because the git ladder here is
/// where every new failure mode lands. Both stay under the file-length rule by not
/// sharing a page.
/// </para>
/// <para>
/// The ladder is: clone one branch at one commit (2026-09-22-b41d), then (when a revision
/// is named) check it out; a checkout that fails asks the host for that one ref and tries
/// again, and a fetch that lands nothing asks once more WITH depth and takes what came back.
/// </para>
/// <para>
/// 2026-09-22-2d11b: the work path is read FIRST, because a sandbox held from an earlier
/// turn already carries this repository and git refuses to clone into a non-empty directory
/// with a message the classifier would read as an unreachable host. A tree already there is
/// brought to the remote's own HEAD — unless the scope names a REVISION, which is a pin the
/// tree is already on and must not be walked off.
/// </para>
/// </summary>
public sealed class SourceScopeMaterialiser(SourceScopeRefresh refresh)
{
    /// <summary>Clones or refreshes, lands on the revision when one is named, reports the sha.</summary>
    public async Task<string> PrepareAsync(
        ISandbox sandbox, RepoConnection repo, string? revision, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(repo);

        if (!await refresh.HoldsRepoAsync(sandbox, repo, ct))
        {
            var clone = await sandbox.RunStepAsync(
                CheckoutStepFactory.BuildScopeCloneStep(repo), null, ct);
            if (clone.ExitCode != 0)
                throw SourceScopeFailures.Fail(SourceScopeFailures.KindOf(clone), repo, revision,
                    $"git clone failed: {SourceScopeFailures.Text(clone)}");
            if (!string.IsNullOrWhiteSpace(revision))
                await LandOnAsync(sandbox, repo, revision!, ct);
        }
        else if (string.IsNullOrWhiteSpace(revision))
        {
            await refresh.ToRemoteHeadAsync(sandbox, repo, ct);
        }

        var head = await sandbox.RunStepAsync(CheckoutStepFactory.BuildResolveHeadStep(), null, ct);
        return head.ExitCode == 0
            ? (head.OutputContent ?? string.Empty).Trim()
            : throw SourceScopeFailures.Fail(SourceScopeFailureKind.NoSuchRevision, repo, revision,
                $"the clone will not say what HEAD is: {SourceScopeFailures.Text(head)}");
    }

    private static async Task LandOnAsync(
        ISandbox sandbox, RepoConnection repo, string revision, CancellationToken ct)
    {
        var first = await sandbox.RunStepAsync(CheckoutStepFactory.BuildCheckoutStep(revision), null, ct);
        if (first.ExitCode == 0) return;

        var fetch = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildFetchRevisionStep(repo, revision), null, ct);
        if (fetch.ExitCode == 0)
        {
            var second = await sandbox.RunStepAsync(
                CheckoutStepFactory.BuildCheckoutStep(revision), null, ct);
            if (second.ExitCode == 0) return;
        }

        // 2026-09-22-b41d: the clone is one branch at one commit, so a revision it does not
        // carry is the ordinary case, and what the fetch above brought is reachable only as
        // FETCH_HEAD — a single-branch clone tracks no other remote ref. Asking WITH depth is
        // the last rung before any refusal, and the two stay apart: a host that hands the
        // revision over neither way is a revision not fetched, never an unreachable host.
        var deepened = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildFetchRevisionAtDepthStep(repo, revision), null, ct);
        if (deepened.ExitCode != 0)
            throw SourceScopeFailures.Fail(SourceScopeFailureKind.RevisionNotFetched, repo, revision,
                "the clone does not carry this revision and the host would not hand it over, "
                + "with depth or without — a sha reachable from no branch and no tag looks "
                + $"like this: {SourceScopeFailures.Text(deepened)}");

        var landed = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildCheckoutStep("FETCH_HEAD"), null, ct);
        if (landed.ExitCode != 0)
            throw SourceScopeFailures.Fail(SourceScopeFailureKind.NoSuchRevision, repo, revision,
                $"the revision does not resolve, even after asking the host for it: {SourceScopeFailures.Text(landed)}");
    }
}
