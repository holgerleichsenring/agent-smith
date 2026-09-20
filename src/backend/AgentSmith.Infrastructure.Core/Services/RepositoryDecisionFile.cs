using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// 2026-09-19-c511a: the repository's copy of the decision log —
/// <c>.agentsmith/decisions/&lt;label&gt;.yaml</c> in the run's checkout, read and written through
/// the SANDBOX the checkout lives in. Its predecessor composed the same path against the sandbox
/// mount /work and gave it to System.IO in the server process, where nothing of the sort exists.
/// <para>
/// 2026-09-19-c511b: it reports rather than throws. A copy nobody could write is a warning; the
/// decision itself is already on the run.
/// </para>
/// </summary>
internal sealed class RepositoryDecisionFile(ILogger logger)
{
    private const string DecisionsDir = ".agentsmith/decisions";

    // Sub-agents of one run share that run's decisions/<runId>.yaml, so the read-append-write is
    // serialised — one semaphore for the process, because the contended file is reachable from
    // several tool hosts and each builds its own reader over the same sandbox.
    //
    // What that costs OTHER runs is bounded HERE rather than by the sandbox: the critical section
    // now holds up to three sandbox round-trips, each a 30-second step the host waits 60 seconds
    // for (SandboxStepCap), so a wedged sandbox could otherwise stall every other run's
    // log_decision for three minutes inside its model call. A run that waits longer than
    // LockWaitSeconds for the lock gives up its COPY, with a warning; the decision is already on
    // the run. A per-file semaphore would remove the coupling and add a dictionary entry per run
    // id that nothing in a month-long server process ever removes.
    private const int LockWaitSeconds = 30;

    // Mirrors SizeLimits.ListFilesMaxEntries — the point at which a listing stops being an answer.
    private const int ListingIsInconclusiveAt = 1000;

    private readonly SemaphoreSlim _lock = new(1, 1);

    internal async Task<bool> AppendAsync(
        ISandboxFileReader files, DecisionFileLabel label,
        DecisionCategory category, string decision, CancellationToken ct)
    {
        var path = $"{DecisionsDir}/{label.FileName}";
        if (!await _lock.WaitAsync(TimeSpan.FromSeconds(LockWaitSeconds), ct))
        {
            logger.LogWarning(
                "Decision recorded on the run but NOT written to {Path}: another write held the "
                + "decision log for more than {Seconds}s", path, LockWaitSeconds);
            return false;
        }
        try
        {
            var existing = await ReadExistingAsync(files, label, path, ct);
            if (existing is null) return false;

            await files.WriteAsync(path, existing + DecisionYamlFormatter.FormatItem(category, decision), ct);
            logger.LogDebug("Logged decision [{File}/{Category}]: {Decision}",
                label.FileName, category, decision);
            return true;
        }
        catch (Exception ex) when (!Cancelled(ex, ct))
        {
            logger.LogWarning(ex,
                "Decision recorded on the run but NOT written to {Path} in the repository "
                + "({Reason}): [{Category}] {Decision}",
                path, ex.Message, category, decision);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// The content to append to, or null when the file exists and could not be read — which is NOT
    /// the same as absent, and the surface cannot tell them apart on its own: TryReadAsync answers
    /// null for every non-zero exit, including a file over the read limit and one that is not
    /// UTF-8. Listing the directory is the discriminator, because it names files without reading
    /// them; replacing a file we failed to read would lose every decision already in it.
    /// </summary>
    private async Task<string?> ReadExistingAsync(
        ISandboxFileReader files, DecisionFileLabel label, string path, CancellationToken ct)
    {
        var listed = await files.ListAsync(DecisionsDir, maxDepth: 1, ct);
        if (listed.Any(entry => Path.GetFileName(entry) == label.FileName))
            return await ReadOrRefuseAsync(files, path, ct);

        // A listing that hit the agent's entry cap is not evidence of absence — it stopped early,
        // in directory order, before any sort. This repository's own decisions directory is past
        // 700 files, so "the file is simply not listed" would eventually mean "the file was not
        // reached", and starting from the header there would erase every decision in it.
        if (listed.Count < ListingIsInconclusiveAt) return label.Header;

        logger.LogWarning(
            "Decision file {Path} could not be looked up — {Count} entries is the listing cap, so "
            + "the answer says nothing; leaving the file as it is", path, listed.Count);
        return null;
    }

    private async Task<string?> ReadOrRefuseAsync(
        ISandboxFileReader files, string path, CancellationToken ct)
    {
        var content = await files.TryReadAsync(path, ct);
        if (content is not null) return content;

        logger.LogWarning(
            "Decision file {Path} exists but could not be read — leaving it as it is rather than "
            + "replacing it with a fresh one", path);
        return null;
    }

    internal static bool Cancelled(Exception ex, CancellationToken ct) =>
        ex is OperationCanceledException && ct.IsCancellationRequested;
}
