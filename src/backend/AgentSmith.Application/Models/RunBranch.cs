using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: the branch a run checks out, together with the one fact that decides whether
/// the framework may write to it.
/// <para>
/// <see cref="ComposedFromTicket"/> is true ONLY when this run derived the name from its
/// own ticket. A branch that arrived on <c>ContextKeys.CheckoutBranch</c> — a pull
/// request's head branch from a review webhook, a scan target, the init branch — belongs
/// to somebody else, and the work-branch push force-pushes with a lease, so writing a
/// merge into one of those would land in a contributor's branch.
/// </para>
/// <para>
/// 2026-09-13-5cdf: <see cref="LabelParentId"/> is where the run sits in the cut, not
/// part of the branch's own name. The base is RESOLVED from it against the clone, never
/// parsed back out of a branch name — a feature segment in the name would rename the
/// branch of every ticket that already has one, and a pull request is found by its head
/// branch, so a re-run after such a rename would abandon the open pull request.
/// </para>
/// <para>
/// 2026-11-17-713ea: <see cref="CutFresh"/> says the branch is cut from its base on every run,
/// whatever the remote already holds — the init branch, so a second init never builds on what
/// an earlier one left there.
/// </para>
/// <para>
/// 2026-11-17-9a39b: <see cref="Stacked"/> names the unlanded prerequisites whose pushed branches are the rungs — found,
/// never published. 2026-11-23-31dca: one per repo (<see cref="ParentFor"/>); a missing one stops the run instead of
/// falling through to the clone's base, and a repo with no entry takes the label parent, as an unstacked run does.
/// </para>
/// <para>2026-11-24-f9c5b: <see cref="SeriesBranch"/> is the branch a ticket of a series with one is cut from in every repo —
/// published at the clone's base by the first ticket there, never missing after that (a missing one stops the run).</para>
/// </summary>
public sealed record RunBranch(
    BranchName Name, bool ComposedFromTicket, string? LabelParentId = null, bool CutFresh = false, StackedBases? Stacked = null,
    string? SeriesBranch = null)
{
    /// <summary>The parent this repo is cut from: its stacked prerequisite, else the series branch, else the label parent.</summary>
    public string? ParentFor(string? repo) => Stacked?.For(repo) ?? SeriesBranch ?? LabelParentId;

    /// <summary>2026-11-24-f9c5b: whether this repo is cut from the series branch.</summary>
    public bool IsSeriesIn(string? repo) => SeriesBranch is not null && Stacked?.For(repo) is null;

    /// <summary>Whether this repo is cut from a stacked prerequisite's branch.</summary>
    public bool IsStackedIn(string? repo) => Stacked?.For(repo) is not null;
}
