using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: the comments on a repository's open pull requests since each one's mark, in
/// order (host time, then host id). A comment newer than its pull request's mark advances the mark
/// by compare-and-set and, when that won, goes to command admission — which ignores what is no
/// command. A host that reads comments per pull request gets a rotating ten per cycle.
/// </summary>
public sealed class PrSweepComments(PrSweepStore store, PrSweepActions actions)
{
    private const int PerCycle = 10;

    public async Task RunAsync(
        SweepTarget target, string key, IReadOnlyList<OpenPullRequest> open, IOpenPullRequestLister lister, int turn, CancellationToken ct)
    {
        var states = await store.StatesAsync(key, ct);
        var prs = lister.ReadsCommentsPerPullRequest ? Window(open, turn) : open;
        var known = prs.Where(p => states.ContainsKey(p.Number)).ToList();
        if (known.Count == 0) return;
        var since = new DateTimeOffset(known.Min(p => states[p.Number].CommentsSeenTicks), TimeSpan.Zero);
        foreach (var comment in (await lister.CommentsSinceAsync(known, since, ct)).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            var mark = states[comment.PrNumber];
            if (!Newer(comment, mark.CommentsSeenTicks, mark.CommentsSeenId)) continue;
            if (!await store.TryAdvanceCommentsAsync(key, comment.PrNumber, mark.CommentsSeenTicks, mark.CommentsSeenId, comment.CreatedAt.UtcTicks, comment.Id, ct))
                continue;
            mark.CommentsSeenTicks = comment.CreatedAt.UtcTicks;
            mark.CommentsSeenId = comment.Id;
            await actions.CommandAsync(target, comment, ct);
        }
    }

    private static bool Newer(PrSweepComment comment, long ticks, string id) =>
        comment.CreatedAt.UtcTicks > ticks || (comment.CreatedAt.UtcTicks == ticks && string.CompareOrdinal(comment.Id, id) > 0);

    private static IReadOnlyList<OpenPullRequest> Window(IReadOnlyList<OpenPullRequest> open, int turn)
    {
        if (open.Count <= PerCycle) return open;
        var start = turn * PerCycle % open.Count;
        return [.. Enumerable.Range(0, PerCycle).Select(i => open[(start + i) % open.Count])];
    }
}
