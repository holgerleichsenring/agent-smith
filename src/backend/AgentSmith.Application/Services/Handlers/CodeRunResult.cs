using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The end of a green CommitAndPR, moved out of the handler (2026-11-17-023fa) so it stays at its baseline: the step's
/// result line and the pull requests a merge may take.
/// </summary>
public static class CodeRunResult
{
    /// <summary>
    /// 2026-11-17-023fa: publishes the opened pull requests as gate-passed when the run changed code, none is a draft
    /// and no repository failed to open — the only pull requests CompletePullRequests ever merges.
    /// </summary>
    public static void PublishGatePassed(PipelineContext pipeline, IReadOnlyList<OpenedPullRequest> opened, bool mergeable)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        if (!mergeable || opened.Any(o => o.Status == OpenStatus.Failed)) return;
        var passed = opened.Where(o => o.Status == OpenStatus.Opened && o.Url is not null).ToList();
        if (passed.Count > 0) pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, passed);
    }

    // p0235: a clear, factual run outcome — this message becomes the step's
    // result line, so it must say plainly what happened (changes + PR, or "no
    // code changes"), not a bare URL.
    public static CommandResult Build(IReadOnlyList<OpenedPullRequest> opened, bool anyCode, int changeCount)
    {
        var openedEntries = opened.Where(o => o.Status == OpenStatus.Opened).ToList();
        var failed = opened.Count(o => o.Status == OpenStatus.Failed);
        if (openedEntries.Count == 0 && failed > 0)
            return CommandResult.Fail($"All {opened.Count} PR open attempts failed.");
        if (openedEntries.Count == 0)
            return CommandResult.Ok("No PR opened (nothing to record).");
        var urls = string.Join(", ", openedEntries.Select(o => o.Url));
        if (!anyCode)
            return CommandResult.Ok(
                $"No code changes were applied — run recorded in PR: {urls} (safe to close).");
        var prWord = openedEntries.Count == 1 ? "PR" : "PRs";
        return CommandResult.Ok($"Completed: {changeCount} file(s) changed — {prWord}: {urls}");
    }
}
