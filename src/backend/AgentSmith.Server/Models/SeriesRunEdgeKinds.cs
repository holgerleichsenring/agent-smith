namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcf: what a requires edge inside a series means for the dependent's branches, from the per-repo
/// stack 2026-11-23-31dca starts by. Stacked: the dependent is cut from the prerequisite's branch (or a branch carrying it)
/// in a repo. Merge: the requirement is met only by a merge — two independent prerequisites worked in one repo, or the
/// project starts dependents only once their requirements landed. Unknown: the prerequisite (or an unlanded one its chain
/// reaches) has no green attempt, so its repos are not known yet and a dependent that never started waits on it. Landed: the prerequisite merged; the default branch carries it.</summary>
public static class SeriesRunEdgeKinds
{
    public const string Stacked = "stacked";
    public const string Merge = "merge";
    public const string Unknown = "unknown";
    public const string Landed = "landed";
}
