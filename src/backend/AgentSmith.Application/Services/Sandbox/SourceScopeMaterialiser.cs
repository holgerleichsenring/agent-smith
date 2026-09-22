using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-9802: puts a freshly spawned read-only sandbox onto the revision it was
/// asked for, and says precisely why when it cannot.
/// <para>
/// It lives outside <see cref="SourceScopeSandbox"/> because that class owns a different
/// job — the read-only guard and the lazy spawn — and because the git ladder here is
/// where every new failure mode lands. Both stay under the file-length rule by not
/// sharing a page.
/// </para>
/// <para>
/// The ladder is: clone one branch at one commit (2026-09-22-b41d), then (when a revision
/// is named) check it out; a checkout that fails asks the host for that one ref and tries
/// again, and a fetch that lands nothing asks once more WITH depth and takes what came
/// back. The refusals are told apart so an operator reads an action, not git.
/// </para>
/// </summary>
public sealed class SourceScopeMaterialiser
{
    /// <summary>
    /// The host answering "no such repository" for a private one it will not admit to is
    /// indistinguishable from a missing repository, and the action is the same either way:
    /// look at the token. Auth is therefore matched BEFORE transport, because git wraps
    /// both in "unable to access".
    /// </summary>
    private static readonly string[] AuthMarkers =
    [
        "authentication failed", "could not read username", "invalid username or password",
        "403", "401", "permission denied", "access denied", "terminal prompts disabled",
        "repository not found",
    ];

    /// <summary>Clones, lands on the revision when one is named, and reports the sha.</summary>
    public async Task<string> PrepareAsync(
        ISandbox sandbox, RepoConnection repo, string? revision, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(repo);

        var clone = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildScopeCloneStep(repo), null, ct);
        if (clone.ExitCode != 0)
            throw Fail(CloneKind(clone), repo, revision, $"git clone failed: {Text(clone)}");

        if (!string.IsNullOrWhiteSpace(revision))
            await LandOnAsync(sandbox, repo, revision!, ct);

        var head = await sandbox.RunStepAsync(CheckoutStepFactory.BuildResolveHeadStep(), null, ct);
        return head.ExitCode == 0
            ? (head.OutputContent ?? string.Empty).Trim()
            : throw Fail(SourceScopeFailureKind.NoSuchRevision, repo, revision,
                $"the clone will not say what HEAD is: {Text(head)}");
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
            throw Fail(SourceScopeFailureKind.RevisionNotFetched, repo, revision,
                "the clone does not carry this revision and the host would not hand it over, "
                + "with depth or without — a sha reachable from no branch and no tag looks "
                + $"like this: {Text(deepened)}");

        var landed = await sandbox.RunStepAsync(
            CheckoutStepFactory.BuildCheckoutStep("FETCH_HEAD"), null, ct);
        if (landed.ExitCode != 0)
            throw Fail(SourceScopeFailureKind.NoSuchRevision, repo, revision,
                $"the revision does not resolve, even after asking the host for it: {Text(landed)}");
    }

    /// <summary>
    /// Auth first, transport second, and transport again for anything unrecognised: git
    /// wraps every remote problem in "unable to access", so the markers are what separate
    /// them, and a clone that failed for a reason neither list knows is still a clone that
    /// did not reach its remote. The raw git text rides along in the message either way.
    /// </summary>
    private static SourceScopeFailureKind CloneKind(StepResult result)
    {
        var text = Text(result).ToLowerInvariant();
        return AuthMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal))
            ? SourceScopeFailureKind.Unauthorised
            : SourceScopeFailureKind.Unreachable;
    }

    private static string Text(StepResult result) =>
        string.Join(" ", new[] { result.ErrorMessage, result.OutputContent }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static SourceScopeUnavailableException Fail(
        SourceScopeFailureKind kind, RepoConnection repo, string? revision, string message) =>
        new(kind, repo.Name, revision,
            $"'{repo.Name}'{(revision is null ? string.Empty : $" at '{revision}'")}: {message}");
}
